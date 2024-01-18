﻿using NeoFPS;
using NeoFPS.ModularFirearms;
using NeoFPS.SinglePlayer;
using System;
using UnityEngine;

namespace Playground
{
    [CreateAssetMenu(fileName = "Health Pickup Recipe", menuName = "Playground/Recipe/Health Pickup", order = 115)]
    public class HealthPickupRecipe : ItemRecipe<HealthPickup>
    {
        [NonSerialized]
        private IHealthManager _healthManager;
        private IHealthManager healthManager
        {
            get
            {
                if (_healthManager == null && FpsSoloCharacter.localPlayerCharacter != null)
                {
                    _healthManager = FpsSoloCharacter.localPlayerCharacter.GetComponent<IHealthManager>();
                }
                return _healthManager;
            }
        }

        public override void Reset()
        {
            _healthManager = null;
            base.Reset();
        }

        /// <summary>
        /// Get the heal amount as a percentage of the characters missing health.
        /// </summary>
        public float healAmountPerCent
        {
            get {
                return (pickup as HealthPickup).GetHealAmount() / (_healthManager.healthMax - _healthManager.health);
            }
        }

        public override bool ShouldBuild
        {
            get
            {
                if (healthManager == null)
                {
                    // Character has not been spawned yet and so we must be in the upgrade level menu
                    return base.ShouldBuild;
                }

                float missingHealth = healthManager.healthMax - healthManager.health;

                return missingHealth > 0 && base.ShouldBuild;
            }
        }
    }
}