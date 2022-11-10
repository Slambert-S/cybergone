﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/**
 * NOTE: Ya de l'asti de sauce qui marche pas avec les layers!!!!
 */
[RequireComponent(typeof(WeaponManager))]
public class PlayerShoot : NetworkBehaviour
{
    public const string PLAYER_TAG = "Player";


    private PlayerWeapon m_CurrentWeapon;
    private WeaponManager m_WeaponManager;
    [SerializeField] private RectTransform crosshair;
    [SerializeField] private Camera cam;
    [SerializeField] private LayerMask mask;

    private bool    m_bMoving;
    private float   m_fMovement;

    // Start is called before the first frame update
    void Start()
    {
        if (cam == null) {
            this.enabled = false;
            throw new Exception("PlayerShoot: No camera attached");
        }

        // Make the gun in the gun layer for the purpose of the weapon camera
        // The weapon camera is responsible of preventing weapon cliping
        m_WeaponManager = GetComponent<WeaponManager>();

        m_CurrentWeapon = m_WeaponManager.GetCurrentWeapon();


        // Load the weapon so the player doesn't need to reload before first using it.
        m_CurrentWeapon.currentLoadedAmmo   = m_CurrentWeapon.magazineSize;
        m_CurrentWeapon.currentSpareAmmo    = m_CurrentWeapon.maxAmmo;
        m_CurrentWeapon.readyToShoot        = true;
    }

    // Update is called once per frame
    void Update() 
    {
        if (crosshair == null)
        {
            crosshair = GameObject.FindObjectOfType<PlayerSetup>().playerUIInstance.GetComponentInChildren<DynamicCrosshair>().GetComponent<RectTransform>();
        }

        m_CurrentWeapon = m_WeaponManager.GetCurrentWeapon();

        // Unify both methods of shooting (semi/full-auto) under one variable
        if (m_CurrentWeapon.allowContinuousFire)
        {
            m_CurrentWeapon.shooting = Input.GetButton("Fire1") || Input.GetButtonDown("Fire1");
        }
        else
        {
            m_CurrentWeapon.shooting = Input.GetButtonDown("Fire1");
        }

        // Reload
        if (Input.GetButtonDown("Reload") && 
            m_CurrentWeapon.currentLoadedAmmo < m_CurrentWeapon.magazineSize && 
            !(m_CurrentWeapon.currentSpareAmmo == 0)&&
            m_CurrentWeapon.reloading == false)
        {
            Debug.Log("Reload");
            Reload();
        }

        // Shoot
        if (m_CurrentWeapon.readyToShoot            && 
            m_CurrentWeapon.shooting                && 
            !m_CurrentWeapon.reloading              && 
            m_CurrentWeapon.currentLoadedAmmo > 0)
        {
            Shoot();
        }

        m_fMovement = Mathf.Clamp(Mathf.Abs(Input.GetAxis("Horizontal")) + Mathf.Abs(Input.GetAxis("Vertical")), 0f, 1f);

        // If not shooting, recover from spread
        if (m_CurrentWeapon.readyToShoot)
        { 
            m_CurrentWeapon.currentSpread -= m_CurrentWeapon.spreadRecovery * Time.deltaTime * 60;
            if (m_CurrentWeapon.currentSpread < m_CurrentWeapon.minSpread)
            {
                m_CurrentWeapon.currentSpread = m_CurrentWeapon.minSpread;
            }

            UpdateCrosshair();
        }
    }

    /// <summary>
    /// Called on the server when the player shoots
    /// </summary>
    [Command]
    void CmdOnShoot() {
        RpcDoShootEffect();
    }

    /// <summary>
    /// Called on the server when we hit something
    /// </summary>
    /// <param name="pos">The hit point</param>
    /// <param name="normal">The hit normal</param>
    [Command]
    void CmdOnHit(Vector3 pos, Vector3 normal) {
        RpcDoHitEffect(pos, normal);
    }

    /// <summary>
    /// Called on all clients when we need to do a
    /// shoot effect
    /// </summary>
    [ClientRpc]
    void RpcDoShootEffect() {
        ParticleSystem particle = Instantiate(
                                        m_WeaponManager.GetCurrentWeaponGraphics().muzzelFlash, 
                                        GetComponentInChildren<ParticleOrigin>().gameObject.transform);

        Destroy(particle.gameObject, 0.5f);
    }

    /// <summary>
    /// Shows the hit effect on all Clients
    /// </summary>
    /// <param name="pos">The hit point</param>
    /// <param name="normal">The surface normal</param>
    [ClientRpc]
    void RpcDoHitEffect(Vector3 pos, Vector3 normal) {
        GameObject temp = (GameObject)Instantiate(m_WeaponManager.GetCurrentWeaponGraphics().hitEffectPrefab, pos, Quaternion.LookRotation(normal));
        Destroy(temp, 2.0f);
    }

    /// <summary>
    /// Sets the weapon to reloading and invokes the ReloadFinished function after the reload time is completed.
    /// </summary>
    [Client]
    void Reload()
    {
        m_CurrentWeapon.reloading = true;
        Debug.Log("Reload Start");
        Invoke("ReloadFinished", m_CurrentWeapon.reloadTime);
    }

    /// <summary>
    /// Reloads the weapon, subtracting from the spareAmmo and refilling the magazine.
    /// </summary>
    [Client]
    void ReloadFinished()
    {
        if (!m_CurrentWeapon.reloading)
        {
            Debug.Log("Reload Aborted.");
        }
        else
        {
            if (m_CurrentWeapon.currentSpareAmmo        >=  m_CurrentWeapon.magazineSize - m_CurrentWeapon.currentLoadedAmmo)
            {
                m_CurrentWeapon.currentSpareAmmo        -=  m_CurrentWeapon.magazineSize - m_CurrentWeapon.currentLoadedAmmo;
                m_CurrentWeapon.currentLoadedAmmo       =   m_CurrentWeapon.magazineSize;
            }
            else if (m_CurrentWeapon.currentSpareAmmo   >   0)
            {
                // If there is not enough spare ammo to fully refill the magazine, partially reload it instead.
                m_CurrentWeapon.currentLoadedAmmo       +=  m_CurrentWeapon.currentSpareAmmo;
                m_CurrentWeapon.currentSpareAmmo        =   0;
            }
            else
            {
                // Infinite Ammo Weapon
                m_CurrentWeapon.currentLoadedAmmo       =   m_CurrentWeapon.magazineSize;
            }

            m_CurrentWeapon.reloading = false;
            Debug.Log("Reload Finished");
        }
    }

    [Client]
    void Shoot()
    {
        m_CurrentWeapon.readyToShoot = false;

        // If there are multiple shots to fire, cast them all at once
        for (int shotNumber = 0; shotNumber < m_CurrentWeapon.shotCount; ++shotNumber)
        {
            // We are shooting call shoot method on Server
            CmdOnShoot();

            float xSpread = UnityEngine.Random.Range(
                                            -m_CurrentWeapon.currentSpread - m_CurrentWeapon.currentSpread * m_fMovement, 
                                            m_CurrentWeapon.currentSpread + m_CurrentWeapon.currentSpread * m_fMovement);
            float ySpread = UnityEngine.Random.Range(
                                            -m_CurrentWeapon.currentSpread - m_CurrentWeapon.currentSpread * m_fMovement, 
                                            m_CurrentWeapon.currentSpread + m_CurrentWeapon.currentSpread * m_fMovement);

            Vector3 shotDirection = cam.transform.forward + new Vector3(xSpread, ySpread, 0);

            RaycastHit hit;
            if (Physics.Raycast(cam.transform.position, shotDirection, out hit, m_CurrentWeapon.maxRange, mask))
            {
                // We hit Something
                if (hit.collider.tag == PLAYER_TAG)
                {
                    int finalDamage = m_CurrentWeapon.damage;
                    if (hit.distance > m_CurrentWeapon.falloffStart)
                    {
                        if (hit.distance < m_CurrentWeapon.falloffMax)
                        {
                            // Damage linearly falls off between the minimum falloff distance and the maximum falloff.
                            float falloffPercent = (hit.distance - m_CurrentWeapon.falloffStart)/(m_CurrentWeapon.falloffMax - m_CurrentWeapon.falloffStart);
                            finalDamage = Mathf.RoundToInt(
                                (falloffPercent * m_CurrentWeapon.falloffDamage) +
                                ((1 - falloffPercent) * m_CurrentWeapon.damage));
                        }
                        else
                        {
                            finalDamage = m_CurrentWeapon.falloffDamage;
                        }
                    }
                    
                    CmdPlayerShot(hit.collider.name, this.name, finalDamage);
                }

                // Play Hit effect on the server
                CmdOnHit(hit.point, hit.normal);
            }
        }

        //Adjust spread
        m_CurrentWeapon.currentSpread += m_CurrentWeapon.spreadIncrease;

        if(m_CurrentWeapon.currentSpread > m_CurrentWeapon.maxSpread)
        {
            m_CurrentWeapon.currentSpread = m_CurrentWeapon.maxSpread;
        }

        UpdateCrosshair();
            
        // Consume ammunition
        --m_CurrentWeapon.currentLoadedAmmo;

        // Indicate the weapon is ready to fire again after the appropriate delay
        Invoke("ReadyToShoot", 1.0f / m_CurrentWeapon.fireRate);
    }

    void UpdateCrosshair()
    {
        crosshair.sizeDelta     = new Vector2(  1000 * (m_CurrentWeapon.currentSpread + m_CurrentWeapon.currentSpread * m_fMovement),
                                                1000 * (m_CurrentWeapon.currentSpread + m_CurrentWeapon.currentSpread * m_fMovement));
    }

    void ReadyToShoot()
    {
        m_CurrentWeapon.readyToShoot = true;
    }

    [Command]
    void CmdPlayerShot(string hit_id, string src, int damage) {
        // Do the damage stuff
        Debug.Log(hit_id + " has been shot");

        // In the future shoud pass the source to grant assists
        Player p = GameManager.GetPlayer(hit_id);
        p.RpcTakeDamage(damage, src);

        Debug.Log(hit_id + " has " + p.GetHealth());
    }
}