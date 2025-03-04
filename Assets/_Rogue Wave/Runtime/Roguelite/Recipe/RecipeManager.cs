﻿using RogueWave.GameStats;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RogueWave
{
    public static class RecipeManager
    {
        public static Dictionary<string, IRecipe> allRecipes = new Dictionary<string, IRecipe>();
        static Dictionary<string, IRecipe> powerupRecipes = new Dictionary<string, IRecipe>();
        static bool isInitialised = false;

        public static void Initialise()
        {
            if (isInitialised)
            {
                return;
            }

            AbstractRecipe[] itemRecipes = Resources.LoadAll<AbstractRecipe>("Recipes");
            foreach (AbstractRecipe recipe in itemRecipes)
            {
                recipe.Reset();

                if (allRecipes.ContainsKey(recipe.UniqueID))
                {
                    Debug.LogError($"Duplicate recipe found for GUID {recipe.UniqueID} the two recipes are {recipe} and {allRecipes[recipe.uniqueID]}. This shouldn't happen.");
                    continue;
                }

                allRecipes.Add(recipe.UniqueID, recipe);
                if (recipe.IsPowerUp)
                {
                    powerupRecipes.Add(recipe.UniqueID, recipe);
                }
            }

            isInitialised = true;
        }

        /// <summary>
        /// Get the recipe that uses the supplied GUID.
        /// </summary>
        /// <param name="GUID"></param>
        /// <param name="recipe">The recipe return value if it exists in the collection of reciped.</param>
        /// <returns></returns>
        public static bool TryGetRecipe(string GUID, out IRecipe recipe)
        {
            if (isInitialised == false)
            {
                Initialise();
            }

            bool success = allRecipes.TryGetValue(GUID, out recipe);

            if (success == false)
            {
                Debug.LogError($"No recipe found for GUID {GUID}. This shouldn't happen. Has someone changed the GUID?");
            }

            return success;
        }



        /// <summary>
        /// Gets a number of upgrade recipes that can be offered to the player.
        /// </summary>
        /// <param name="quantity">The number of upgrades to offer.</param>
        /// <param name="requiredWeaponCount">The number of weapons that must be offered.</param>
        /// <returns>An array of recipes that can be offered to the player.</returns>
        internal static List<IRecipe> GetOffers(int quantity, int requiredWeaponCount)
        {
            if (isInitialised == false)
            {
                Initialise();
            }

            List<IRecipe> offers = new List<IRecipe>();

            // Are we required to offer a weapon?
            List<WeaponRecipe> weaponCandidates = null;
            if (requiredWeaponCount > 0)
            {
                weaponCandidates = GetOfferCandidates<WeaponRecipe>(false);

                // TODO: Use the weights to select the best weapon to offer
                int idx = Random.Range(0, weaponCandidates.Count);
                for (int i = 0; i < weaponCandidates.Count; i++)
                {
                    if (weaponCandidates[idx].ShouldBuild)
                    {
                        offers.Add(weaponCandidates[idx]);
                        quantity--;
                        requiredWeaponCount--;
                        break;
                    }
                }

                if (offers.Count == 0)
                {
                    // there aren't any weapons that can be offered to the player.
                    return GetOffers(quantity, 0);
                }
            }

            List<IRecipe> candidates = GetOfferCandidates<IRecipe>(true);
            if (weaponCandidates != null)
            {
                candidates.RemoveAll(c => offers.Any(o => o.UniqueID == c.UniqueID));
            }

            WeightedRandom<IRecipe> weights = new WeightedRandom<IRecipe>();

            foreach (IRecipe candidate in candidates)
            {
                // TODO: calculate weights based on the player's current state and the recipe's attributes.
                weights.Add(candidate, candidate.weight);
            }

            for (int i = 0; i < quantity; i++)
            {
                if (weights.Count == 0)
                {
                    break;
                }

                IRecipe recipe = weights.GetRandom();
                offers.Add(recipe);
                weights.Remove(recipe);
            }

            return offers;
        }

        /// <summary>
        /// Get a list of powerup recipes that can be offered to the player.
        /// </summary>
        /// <param name="allowUnaffordable">If true, the player can be offered recipes that they cannot afford.</param>
        /// <returns>A list of possible offers. They have not yet been given weights.</returns>
        private static List<T> GetOfferCandidates<T>(bool allowUnaffordable) where T : IRecipe
        {
            // TODO: cache the results of this search. Invalidate the cache when a new recipe is added to the NanobotManager.
#if UNITY_EDITOR
            Debug.Log($"Getting offer candidates for {typeof(T)}." +
                $"\nNanobot level: {RogueLiteManager.persistentData.currentNanobotLevel}" +
                $"\nPowerup recipes: {powerupRecipes.Count}" +
                $"\nResources: {GameStatsManager.Instance.GetIntStat("RESOURCES")}");
#endif

            List<T> candidates = new List<T>();
            // OPTIMIZATION: We filter on `recipe.IsAvailable` it wouild be more efficient to split the recipes into `availablePowerupRecipes` and `unavailablePowerUpRecipes` on load, this would also allow us to have recipes that have dependencies in the `unavailablePowerups` list until dependencies are satisfied
            foreach (IRecipe recipe in powerupRecipes.Values)
            {
                if (!recipe.IsAvailable)
                {
                    continue;
                }

                if (!allowUnaffordable && GameStatsManager.Instance.GetIntStat("RESOURCES").value < recipe.BuyCost)
                {
#if UNITY_EDITOR
                    //Debug.Log($"Skip: {recipe} is too expensive for the player at a cost of {recipe.BuyCost}.");
#endif
                    continue;
                }

                if (RogueLiteManager.persistentData.currentNanobotLevel < recipe.Level)
                {
#if UNITY_EDITOR
                    //Debug.Log($"Skip: {recipe} level of {recipe.Level} is higher than the current nanobot level of {RogueLiteManager.persistentData.currentNanobotLevel}.");
#endif
                    continue;
                }

                if (recipe is not T)
                {
#if UNITY_EDITOR
                    //Debug.Log($"Skip: {recipe} is not of type {typeof(T)}.");
#endif
                    continue;
                } 

                if (recipe.CanOffer == false)
                {
#if UNITY_EDITOR
                    //Debug.Log($"Skip: {recipe} is not available for offer.");
#endif
                    continue;
                }

#if UNITY_EDITOR
                //Debug.Log($"Offer candidate: {recipe}, cost is {recipe.BuyCost} and weight is {recipe.weight}.");
#endif

                candidates.Add((T)recipe);
            }

#if UNITY_EDITOR
            //string listOfCandidates = "";
            //foreach (T candidate in candidates)
            //{
            //    listOfCandidates += $"\t{candidate} with weight {candidate.weight} and cost of {candidate.BuyCost}.\n";
            //}
            //Debug.Log($"Offer candidates: {candidates.Count}\n{listOfCandidates}");
#endif
            return candidates;
        }
    }
}
