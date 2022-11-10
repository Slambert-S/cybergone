﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Pistol : PlayerWeapon
{
    public Pistol()
    {
        weaponType              = WeaponType.Sidearm;
        damage                  = 12;
        currentLoadedAmmo       = 10;
        magazineSize            = 10;
        currentSpareAmmo        = -1;
        maxAmmo                 = -1;
        shotCount               = 1;
        burstCount              = 1;
        burstIndex              = 0;
        burstFireRate           = 1;
        critMultiplier          = 2.0f;
        falloffStart            = 40f;
        falloffMax              = 60f;
        falloffDamage           = 8;
        maxRange                = 200f;
        fireRate                = 10.0f;
        currentSpread           = 0.0f;
        minSpread               = 0.01f;
        maxSpread               = 0.10f;
        spreadIncrease          = 0.03f;
        spreadRecovery          = 0.005f;
        movementSpread          = 1.5f;
        reloadTime              = 1.0f;
        drawTime                = 0.3f;
        stowTime                = 0.4f;
        allowContinuousFire     = false;
        reloading               = false;
        readyToShoot            = true;
        shooting                = false;

        //burstInfo               = new BurstInfo();
        model                   = WeaponManager.msWeaponArr[(int)weaponType];
    }

    public new GameObject GetModel()
    {
        return model;
    }
}