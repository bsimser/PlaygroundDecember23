using NaughtyAttributes;
using NeoFPS;
using NeoFPS.ModularFirearms;
using NeoFPS.SinglePlayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using RogueWave.GameStats;
using Random = UnityEngine.Random;
using UnityEngine.SceneManagement;
using System;
using WizardsCode.RogueWave;

namespace RogueWave
{
    public class NanobotManager : MonoBehaviour
    {
        public enum ResourceType
        {
            Refined,
            Crystal
        }
        enum voiceDescriptionLevel { Silent, Low, Medium, High }

        // Building
        [SerializeField, Tooltip("A modifier curve used to change various values in the NanobotManager based on game difficulty, such as build cooldown and build speed."), BoxGroup("Building")]
        private AnimationCurve difficultyModifier = AnimationCurve.Linear(0, 1, 1, 1);
        [SerializeField, Tooltip("A modifier for the build time. This can be used by upgrades and buffs to decrease or increase build times."), BoxGroup("Building")]
        private float buildTimeModifier = 1;
        [SerializeField, Tooltip("Cooldown between recipe builds."), BoxGroup("Building")]
        private float buildingCooldown = 4;
        [SerializeField, Tooltip("The resources needed to reach the next nanobot level."), CurveRange(0, 100, 99, 50000, EColor.Green), BoxGroup("Building")]
        private AnimationCurve resourcesForLevel;
        [SerializeField, Tooltip("How far away from the player will built pickup be spawned."), BoxGroup("Building")]
        private float pickupSpawnDistance = 3;

        // Feedbacks
        [SerializeField, Tooltip("Define how 'chatty' the nanobots are. This will affect how often and in what level of detail they announce things to the player."), BoxGroup("Feedbacks")]
        private voiceDescriptionLevel voiceDescription = voiceDescriptionLevel.High;
        [SerializeField, Tooltip("The sound to play to indicate a new recipe is available from home planet. This will be played before the name of the recipe to tell the player that they can call it in if they want."), BoxGroup("Feedbacks")]
        private AudioClip[] recipeRequestPrefix;
        [SerializeField, Tooltip("The sound to play to indicate a recipe has been requested."), BoxGroup("Feedbacks")]
        private AudioClip[] recipeRequested;
        [SerializeField, Tooltip("The sound to play to indicate a recipe request has been queued. This will be played if the player requests the recipe, but the nanobots are busy with something else at that time."), BoxGroup("Feedbacks")]
        private AudioClip[] recipeRequestQueued;
        [SerializeField, Tooltip("The sound to play to indicate a new recipe has been recieved. This will be played before the name of the recipe to tell the player that the recipe has been recieved."), BoxGroup("Feedbacks")]
        private AudioClip[] recipeRecievedPrefix;
        [SerializeField, Tooltip("The sound to play when the build is started. Note that this can be overridden in the recipe."), BoxGroup("Feedbacks")]
        private AudioClip[] buildStartedClips;
        [SerializeField, Tooltip("The sound to play when the build is complete. Note that this can be overridden in the recipe."), BoxGroup("Feedbacks")]
        private AudioClip[] buildCompleteClips;
        [SerializeField, Tooltip("The default particle system to play when a pickup is spawned. Note that this can be overridden in the recipe."), FormerlySerializedAs("pickupSpawnParticlePrefab"), BoxGroup("Feedbacks")]
        ParticleSystem defaultPickupParticlePrefab;
        [SerializeField, Tooltip("The default audio clip to play when a recipe name is needed, but the recipe does not have a name clip. This should never be used in practice."), BoxGroup("Feedbacks")]
        AudioClip defaultRecipeName;

        // Game Stats
        [SerializeField, Tooltip("The current total resources available to the player."), BoxGroup("Game Stats")]
        private IntGameStat m_Resources;
        [SerializeField, Tooltip("The unrefined crystal resources gathered."), BoxGroup("Game Stats")]
        private IntGameStat m_CrystalResourcesCollected;
        [SerializeField, Tooltip("The GameStat to increment when a recipe is called in during a run."), BoxGroup("Game Stats")]
        internal IntGameStat m_RecipesCalledInStat;
        [SerializeField, Tooltip("The GameStat to store the maximum nanobot level the player has attained."), BoxGroup("Game Stats")]
        internal IntGameStat m_MaxNanobotLevelStat;

        [SerializeField, Tooltip("Turn on debug features for the Nanobot Manager"), Foldout("Debug")]
        bool isDebug = false;
#if UNITY_EDITOR
        [SerializeField, Tooltip("A recipe to offer when upgrading the nanobots to the next level. This is used in conjunction with the Level Up button below for testing. This is only used in the editor to test the level up process."), Foldout("Debug"), ShowIf("isDebug")]
        AbstractRecipe _UpgradeRecipe;
#endif

        private List<ArmourRecipe> armourRecipes = new List<ArmourRecipe>();
        private List<HealthPickupRecipe> healthRecipes = new List<HealthPickupRecipe>();
        private List<ShieldRecipe> shieldRecipes = new List<ShieldRecipe>();
        private List<WeaponRecipe> weaponRecipes = new List<WeaponRecipe>();
        private List<AmmoRecipe> ammoRecipes = new List<AmmoRecipe>();
        private List<AmmunitionEffectUpgradeRecipe> ammoUpgradeRecipes = new List<AmmunitionEffectUpgradeRecipe>();
        private List<ToolRecipe> toolRecipes = new List<ToolRecipe>();
        private List<ItemRecipe> itemRecipes = new List<ItemRecipe>();
        private List<PassiveItemRecipe> passiveRecipes = new List<PassiveItemRecipe>();

        private int numInGameRewards = 3;
        internal int resourcesForNextNanobotLevel = 0;

        public delegate void OnRequestSent(IRecipe recipe);
        public event OnRequestSent onRequestSent;

        public delegate void OnBuildStarted(IRecipe recipe);
        public event OnBuildStarted onBuildStarted;

        public delegate void OnNanobotLevelUp(int level, int resourcesForNextLevel);
        public event OnNanobotLevelUp onNanobotLevelUp;

        public delegate void OnStatusChanged(Status status);
        public event OnStatusChanged onStatusChanged;

        public delegate void OnOfferChanged(IRecipe[] offers);
        public event OnOfferChanged onOfferChanged;

        float earliestTimeOfNextItemSpawn = 0;
        private Coroutine rewardCoroutine;

        public enum Status
        {
            Collecting,
            OfferingRecipe,
            Requesting,
            RequestRecieved
        }

        Status _status;
        public Status status
        {
            get { return _status; }
            set
            {
                if (_status == value)
                    return;

                _status = value;

                if (onStatusChanged != null)
                    onStatusChanged(_status);
            }
        }

        IRecipe[] _currentOffers;
        public IRecipe[] currentOfferRecipes
        {
            get { return _currentOffers; }
            set
            {
                if (_currentOffers == value)
                    return;

                _currentOffers = value;

                if (onOfferChanged != null)
                    onOfferChanged(_currentOffers);
            }
        }

        private FpsInventorySwappable _inventory;
        private FpsInventorySwappable inventory
        {
            get
            {
                if (!_inventory && FpsSoloCharacter.localPlayerCharacter != null)
                {
                    _inventory = FpsSoloCharacter.localPlayerCharacter.inventory as FpsInventorySwappable;
                }
                return _inventory;
            }
        }

        private float timeOfLastRewardOffer = 0;

        private float timeOfNextBuiild = 0;
        private bool isBuilding;
        private bool inVictoryRoutine;

        private void OnEnable()
        {
            GetComponent<BasicHealthManager>().onIsAliveChanged += OnPlayerIsAliveChanged;

            RogueWaveGameMode.onLevelComplete += OnLevelComplete;
            RogueWaveGameMode.onPortalEntered += OnPortalEntered;
            resourcesForNextNanobotLevel = GetRequiredResourcesForNextNanobotLevel();
            inVictoryRoutine = false;
        }

        private void OnDisable()
        {
            GetComponent<BasicHealthManager>().onIsAliveChanged -= OnPlayerIsAliveChanged;

            RogueWaveGameMode.onLevelComplete -= OnLevelComplete;
            RogueWaveGameMode.onPortalEntered -= OnPortalEntered;
        }

        private void OnPlayerIsAliveChanged(bool alive)
        {
            if (!alive)
            {
                RogueLiteManager.persistentData.currentNanobotLevel = 1;
                stackedLevelUps = 0;
            }
        }

        private void OnPortalEntered()
        {
            inVictoryRoutine = true;
        }

        private void OnLevelComplete()
        {
            inVictoryRoutine = true;
        }

        private void Start()
        {
            currentOfferRecipes = new IRecipe[numInGameRewards];

            resourcesForNextNanobotLevel = GetRequiredResourcesForNextNanobotLevel();

            // When the player dies the nanobots are reset to level 1, but the player will have a bunch of resources to spend on the next run.
            // We need to calculate how many level ups the nanobots should get with the resources they have.
            // This gives the player a headstart on the next run.
            if (RogueLiteManager.persistentData.currentNanobotLevel == 1)
            {
                int resourcesAvailable = GameStatsManager.Instance.GetIntStat("RESOURCES").value;
                int resourcesForNextLevel = Mathf.RoundToInt(resourcesForLevel.Evaluate(stackedLevelUps + 1));
                while (resourcesAvailable > resourcesForNextLevel)
                {
                    resourcesAvailable -= resourcesForNextLevel;
                    stackedLevelUps++;

                    resourcesForNextLevel = Mathf.RoundToInt(resourcesForLevel.Evaluate(stackedLevelUps + 1));
                }
            }
            nextLevelUpTime = Time.timeSinceLevelLoad + 10;

            // Ensure the recipes are not carrying over any adjustments from previous runs.
            foreach (var healthRecipe in healthRecipes)
            {
                healthRecipe.Reset();
            }
            foreach (var weaponRecipe in weaponRecipes)
            {
                weaponRecipe.Reset();
            }
            foreach (var ammoRecipe in ammoRecipes)
            {
                ammoRecipe.Reset();
            }
            foreach (var ammoRecipe in ammoUpgradeRecipes)
            {
                ammoRecipe.Reset();
            }
            foreach (var toolRecipe in toolRecipes)
            {
                toolRecipe.Reset();
            }
            foreach (var itemRecipe in itemRecipes)
            {
                itemRecipe.Reset();
            }

            // If this is the combat scene then ensure there is a weapon in hand
            if (SceneManager.GetActiveScene().name != RogueLiteManager.combatScene)
            {
                foreach (string guid in RogueLiteManager.persistentData.WeaponBuildOrder)
                {
                    RecipeManager.TryGetRecipe(guid, out IRecipe recipe);
                    m_Resources.Add(recipe.BuildCost);
                    TryRecipe(recipe);
                }
            }
        }

        private void Update()
        {
            if (!FpsSoloCharacter.localPlayerCharacter.isAlive)
            {
                return;
            }

            // Are we leveling up?
            if (resourcesForNextNanobotLevel <= 0)
            {
                stackedLevelUps++;
                LevelUp();
            } else if (stackedLevelUps > 0 && Time.timeSinceLevelLoad > nextLevelUpTime)
            {
                LevelUp();
            }

            if (inVictoryRoutine || isBuilding)
            {
                return;
            }

            if (timeOfNextBuiild > Time.timeSinceLevelLoad)
            {
                return;
            }

            // Prioritize building ammo if the player is low
            if (TryAmmoRecipes(0.2f))
            {
                return;
            }

            // Health is the next priority, got to stay alive, but only build at this stage if fairly badly hurt
            if (TryHealthRecipes(0.7f))
            {
                return;
            }

            // If any weapon has zero ammo then build ammo for it
            //if (TryAllAmmoRecipes())
            //{
            //    return;
            //}

            // Prioritize building shield if the player does not have a shield or is low on shield
            if (TryShieldRecipes(0.4f))
            {
                return;
            }

            // Prioritize building armour if the player does not have armour or is low on armour
            if (TryArmourRecipes(0.2f))
            {
                return;
            }

            // If we are in good shape then see if there's a new weapon we can build
            if (TryWeaponRecipes())
            {
                return;
            }

            // Health is the next priority, got to stay alive, but only build at this stage we should only need a small topup
            if (TryHealthRecipes(0.85f))
            {
                return;
            }

            // If we can't afford a powerup, build ammo up to near mazimum (not maximum because there will often be half used clips lying around)
            if (TryAmmoRecipes(0.9f))
            {
                return;
            }

            // If we are in good shape then see if there is a generic item we can build
            if (TryItemRecipes())
            {
                return;
            }

            // Prioritize building shield if the player does not have a shield or there is some damage to the shiled
            if (TryShieldRecipes(0.8f))
            {
                return;
            }

            // Prioritize building armour if the player does not have full armour
            if (TryArmourRecipes(0.9f))
            {
                return;
            }

            // May as well get to maximum health if there is nothing else to build
            if (TryHealthRecipes(1f))
            {
                return;
            }
        }

        internal List<AmmunitionEffectUpgradeRecipe> GetAmmunitionEffectUpgradesFor(SharedAmmoType ammoType)
        {   
            List<AmmunitionEffectUpgradeRecipe> matchingRecipes = new List<AmmunitionEffectUpgradeRecipe>();

            foreach (var recipe in ammoUpgradeRecipes)
            {
                if (recipe.ammoType == ammoType)
                {
                    matchingRecipes.Add(recipe);
                }
            }

            return matchingRecipes;
        }

        IEnumerator OfferInGameRewardRecipe()
        {
            timeOfLastRewardOffer = Time.timeSinceLevelLoad;
            int weapons = 0;
            if (RogueLiteManager.persistentData.WeaponBuildOrder.Count < 3)
            {
                weapons = 1;
            }
            List<IRecipe> offers = RecipeManager.GetOffers(numInGameRewards, weapons);

            if (offers.Count == 0)
            {
                yield break;
            }

            currentOfferRecipes = offers.ToArray();
#if UNITY_EDITOR
            if (isDebug && _UpgradeRecipe != null)
            {
                currentOfferRecipes[0] = _UpgradeRecipe;
            }
#endif
            status = Status.OfferingRecipe;
            yield return null;

            // Announce a recipe is available
            AudioClip clip = recipeRequestPrefix[Random.Range(0, recipeRequestPrefix.Length)];
            AudioClip[] recipeNames = new AudioClip[currentOfferRecipes.Length];
            for (int i = 0; i < currentOfferRecipes.Length; i++) {
                if (currentOfferRecipes[i].NameClip == null)
                {
                    recipeNames[i] = defaultRecipeName;
                    Debug.LogWarning($"Recipe {currentOfferRecipes[i].DisplayName} (offer) does not have an audio clip for its name. Used default name.");
                }
                else
                {
                    recipeNames[i] = currentOfferRecipes[i].NameClip;
                }   

                GameLog.Info($"Offering in-run recipe reward {currentOfferRecipes[i].DisplayName}");
            }
            StartCoroutine(Announce(clip, recipeNames));

            status = Status.OfferingRecipe;

            while (true)
            {
                int i = int.MaxValue;
                // TODO: add these keys to the NeoFPS input manager so that it is configurable
                if (currentOfferRecipes.Length >= 1 && Input.GetKey(KeyCode.B))
                {
                    i = 0;
                } else if (currentOfferRecipes.Length >= 2 && Input.GetKey(KeyCode.N))
                {
                    i = 1;
                } else if (currentOfferRecipes.Length >= 3 && Input.GetKey(KeyCode.M))
                {
                    i = 2;
                }

                if (i < int.MaxValue)
                {
                    status = Status.Requesting;
                    timeOfNextBuiild = Time.timeSinceLevelLoad + currentOfferRecipes[i].TimeToBuild + 5f;

                    // Announce request made
                    GameLog.Info($"Requesting in-run recipe reward {currentOfferRecipes[i].DisplayName}");
                    clip = recipeRequested[Random.Range(0, recipeRequested.Length)];
                    if (Time.timeSinceLevelLoad - timeOfLastRewardOffer > 5)
                    {
                        StartCoroutine(Announce(clip));
                    }

                    onRequestSent?.Invoke(currentOfferRecipes[i]);
                    yield return new WaitForSeconds(currentOfferRecipes[i].TimeToBuild);


                    // Announce request recieved
                    status = Status.RequestRecieved;
                    clip = recipeRecievedPrefix[Random.Range(0, recipeRecievedPrefix.Length)];
                    Announce(clip);
                    yield return Announce(clip);

                    RogueLiteManager.runData.Add(currentOfferRecipes[i]);
                    AddToRunRecipes(currentOfferRecipes[i]);

                    m_RecipesCalledInStat.Add(1);
                    
                    status = Status.Collecting;

                    break;
                }

                yield return null;
            }

            rewardCoroutine = null;
            nextLevelUpTime = Time.timeSinceLevelLoad + 5;
        }

        int stackedLevelUps = 0;
        private float nextLevelUpTime;

        [Button("Level Up the Nanobots")]
        private void LevelUp()
        {
            if (rewardCoroutine != null || stackedLevelUps == 0) // if already offering rewards we need to wait until player has selected an upgrade. If there is no stacked level up we shouldn't be here at all.
            {
                resourcesForNextNanobotLevel = GetRequiredResourcesForNextNanobotLevel();
                return;
            }

            stackedLevelUps--;
            RogueLiteManager.persistentData.currentNanobotLevel++;

            resourcesForNextNanobotLevel = GetRequiredResourcesForNextNanobotLevel();
            onNanobotLevelUp?.Invoke(RogueLiteManager.persistentData.currentNanobotLevel, resourcesForNextNanobotLevel);

            if (!inVictoryRoutine)
            {
                rewardCoroutine = StartCoroutine(OfferInGameRewardRecipe());
            }

            if (m_MaxNanobotLevelStat != null && m_MaxNanobotLevelStat.value < RogueLiteManager.persistentData.currentNanobotLevel)
            {
                m_MaxNanobotLevelStat.Add(1);
            }

            GameLog.Info($"Nanobot level up to {RogueLiteManager.persistentData.currentNanobotLevel}");
        }

        /// <summary>
        /// Make an announcement to the player. This will play the clip at the position of the nanobot manager.
        /// </summary>
        /// <param name="mainClip">The main clip to play</param>
        private IEnumerator Announce(AudioClip mainClip)
        {
            yield return Announce(mainClip, new AudioClip[] { });
        }

        /// <summary>
        /// Make an announcement to the player. This will play the clip at the position of the nanobot manager.
        /// </summary>
        /// <param name="mainClip">The main clip to play</param>
        /// <param name="recipeName">OPTIONAL: if not null then this recipe name clip will be announced after the main clip</param>
        private IEnumerator Announce(AudioClip mainClip, AudioClip recipeName)
        {
            yield return Announce(mainClip, recipeName == null ? null : new AudioClip[] { recipeName });
        }

        /// <summary>
        /// Make an announcement to the player. This will play the clip at the position of the nanobot manager.
        /// </summary>
        /// <param name="mainClip">The main clip to play</param>
        /// <param name="recipeName">OPTIONAL: if not null then these recipe name clips will be announced after the main clip</param>
        private IEnumerator Announce(AudioClip mainClip, AudioClip[] recipeNames)
        {
            if (voiceDescription == voiceDescriptionLevel.Silent)
            {
                yield break;
            }

            //Debug.Log($"Announcing {mainClip.name} with recipe name {recipeName?.name}");

            AudioManager.PlayNanobotOneShot(mainClip);
            yield return new WaitForSeconds(mainClip.length);

            if (recipeNames == null || recipeNames.Length == 0 || voiceDescription < voiceDescriptionLevel.Medium)
            {
                yield break;
            }

            foreach (AudioClip recipeName in recipeNames)
            {
                if (recipeName == null)
                {
                    Debug.LogWarning($"Attempting to announce a null recipe name {recipeName} on {this.name}.");
                    continue;
                }
                else
                {
                    AudioManager.PlayNanobotOneShot(recipeName);
                    yield return new WaitForSeconds(recipeName.length + 0.3f);
                }
            }
        }

        private int GetRequiredResourcesForNextNanobotLevel()
        {
            return Mathf.RoundToInt(resourcesForLevel.Evaluate(RogueLiteManager.persistentData.currentNanobotLevel + stackedLevelUps + 1));
        }

        // TODO: we can probably generalize these Try* methods now that we have refactored the recipes to use interfaces/Abstract classes

        /// <summary>
        /// Build the best health recipe available. The best is the one that will heal to MaxHealth with the minimum waste.
        /// </summary>
        /// <param name="minimumHealthAmount">The % (0-1) of health that is the minimum required before health will be built</param>
        /// <returns></returns>
        private bool TryHealthRecipes(float minimumHealthAmount = 1)
        {
            HealthPickupRecipe chosenRecipe = null;
            float chosenAmount = 0;
            for (int i = 0; i < healthRecipes.Count; i++)
            {
                if (healthRecipes[i].HasAmount(minimumHealthAmount))
                {
                    return false;
                }

                if (m_Resources.value >= healthRecipes[i].BuildCost && healthRecipes[i].ShouldBuild)
                {
                    float healAmount = Mathf.Min(1, healthRecipes[i].healAmountPerCent);
                    if (healAmount > chosenAmount)
                    {
                        chosenRecipe = healthRecipes[i];
                        chosenAmount = healAmount;
                    } else if (healAmount == chosenAmount && (chosenRecipe == null || (chosenRecipe != null && chosenRecipe.BuyCost > healthRecipes[i].BuyCost)))
                    {
                        chosenRecipe = healthRecipes[i];
                        chosenAmount = healAmount;
                    }
                }
            }

            if (chosenRecipe != null)
            {
                return TryRecipe(chosenRecipe);
            }

            return false;
        }

        private bool TryShieldRecipes(float minimumShieldAmount)
        {
            for (int i = 0; i < shieldRecipes.Count; i++)
            {
                if (!shieldRecipes[i].HasAmount(minimumShieldAmount))
                {
                    if (TryRecipe(shieldRecipes[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryArmourRecipes(float minimumArmourAmount)
        {
            for (int i = 0; i < armourRecipes.Count; i++)
            {
                if (!armourRecipes[i].HasAmount(minimumArmourAmount))
                {
                    if (TryRecipe(armourRecipes[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryWeaponRecipes()
        {
            // Build weapons in the build order first
            foreach (string id in RogueLiteManager.persistentData.WeaponBuildOrder)
            {
                IRecipe weapon;
                if (RecipeManager.TryGetRecipe(id, out weapon))
                {
                    if (TryRecipe(weapon as WeaponRecipe))
                    {
                        return true;
                    }
                }
            }

            // If we have built everything in the build order then try to build anything we bought during this run
            foreach (IRecipe recipe in RogueLiteManager.runData.GetRecipes())
            {
                WeaponRecipe weapon = recipe as WeaponRecipe;
                if (weapon != null && RogueLiteManager.persistentData.RecipeIds.Contains(weapon.uniqueID) == false)
                {
                    if (TryRecipe(weapon))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryItemRecipes()
        {
            if (earliestTimeOfNextItemSpawn > Time.timeSinceLevelLoad)
            {
                return false;
            }

            float approximateFrequency = 10;
            for (int i = 0; i < itemRecipes.Count; i++)
            {
                // TODO: make a decision on whether to make a generic item in a more intelligent way
                // TODO: can we make tests that are dependent on the pickup, e.g. when the pickup is triggered it will only be picked up if needed 
                if (m_Resources.value < itemRecipes[i].BuildCost)
                {
                    continue;
                }

                if (TryRecipe(itemRecipes[i]))
                {
                    earliestTimeOfNextItemSpawn = Time.timeSinceLevelLoad + (approximateFrequency * Random.Range(0.7f, 1.3f));
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// If the ammo available for the currently equipped weapon is below the minimum level, try to build ammo.
        /// </summary>
        /// <param name="minimumAmmoAmount">The % (0-1) of ammo that is the minimum required</param>
        /// <returns></returns>
        private bool TryAmmoRecipes(float minimumAmmoAmount)
        {
            SharedPoolAmmo ammoPoolUnderTest = inventory.selected.GetComponent<SharedPoolAmmo>();

            // First check the ammo for the currently selected weapon
            if (inventory.selected != null)
            {
                for (int i = 0; i < ammoRecipes.Count; i++)
                {
                    if (ammoRecipes[i].ammo.itemIdentifier == ammoPoolUnderTest.ammoType.itemIdentifier
                        && ammoPoolUnderTest.currentAmmo <= ammoRecipes[i].ammo.maxQuantity * minimumAmmoAmount)
                    {
                        if (m_Resources.value >= ammoRecipes[i].BuildCost && ammoRecipes[i].ShouldBuild)
                        {
                            return TryRecipe(ammoRecipes[i]);
                        }
                    }
                }
            }

            // if we have enough ammo for the currently selected weapon then check ammo for all other weapons in the inventory
            for (int i = 0; i < inventory.numSlots; i++)
            {
                if (inventory.GetSlotItem(i) != inventory.selected
                    && inventory.GetSlotItem(i) is FpsInventoryWieldableSwappable wieldable)
                {
                    ammoPoolUnderTest = wieldable.GetComponent<SharedPoolAmmo>();
                    
                    if (ammoPoolUnderTest != null && ammoPoolUnderTest.currentAmmo == 0)
                    {
                        for (int j = 0; j < ammoRecipes.Count; j++)
                        {
                            if (ammoRecipes[j].ammo.itemIdentifier == ammoPoolUnderTest.ammoType.itemIdentifier)
                            {
                                if (m_Resources.value >= ammoRecipes[j].BuildCost && ammoRecipes[j].ShouldBuild)
                                {
                                    return TryRecipe(ammoRecipes[j]);
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// If ammo for any weapon in the inventory is zero then try build more.
        /// </summary>
        /// <returns></returns>
        //private bool TryAllAmmoRecipes()
        //{
        //    // iterate over all the ammo recipes and try to build any that are needed
        //    for (int i = 0; i < ammoRecipes.Count; i++)
        //    {
        //        if (ammoRecipes[i].ShouldBuild)
        //        {
        //            if (ammoRecipes[i].HasAmount(0))
        //            {
        //                continue;
        //            }

        //        }
        //    }
            
        //}

        private bool TryRecipe(IRecipe recipe)
        {
            if (recipe == null)
            {
                Debug.LogError("Attempting to build a null recipe.");
                return false;
            }

            if (m_Resources.value < recipe.BuildCost) 
            {
                return false;
            }

            if (recipe.ShouldBuild)
            {
                StartCoroutine(BuildRecipe(recipe));
                return true;
            }

            return false;
        }

        internal IEnumerator BuildRecipe(IRecipe recipe)
        {
            isBuilding = true;
            m_Resources.Subtract(recipe.BuildCost);

            if (recipe.BuildStartedClip != null)
            {
                StartCoroutine(Announce(recipe.BuildStartedClip));
            } else
            {
                AudioClip recipeName = recipe.NameClip;
                if (recipeName == null)
                {
                    recipeName = defaultRecipeName;
                    Debug.LogError($"Recipe {recipe.DisplayName} ({recipe}) does not have an audio clip for its name. Used default of `Unkown`.");
                }

                StartCoroutine(Announce(buildStartedClips[Random.Range(0, buildStartedClips.Length)], recipeName));
            }

            GameLog.Info($"Building {recipe.DisplayName}");

            onBuildStarted?.Invoke(recipe);
            yield return new WaitForSeconds(recipe.TimeToBuild * buildTimeModifier * difficultyModifier.Evaluate(FpsSettings.playstyle.difficulty));

            IItemRecipe itemRecipe = recipe as IItemRecipe;
            if (itemRecipe != null)
            {
                // TODO: should use the PoolManager
                GameObject go = Instantiate(itemRecipe.Item.gameObject);

                Vector3 position = transform.position + (transform.forward * pickupSpawnDistance) + (transform.up * 1f);
                int positionCheck = 0;
                while (Physics.CheckSphere(position, 0.5f) || positionCheck > 10)
                {
                    positionCheck++;
                    position -= transform.forward;
                }

                go.transform.position = position;

                if (recipe.PickupParticles != null)
                {
                    ParticleSystem ps = Instantiate(recipe.PickupParticles, go.transform);
                    ps.Play();
                }
                else if (defaultPickupParticlePrefab != null)
                {
                    ParticleSystem ps = Instantiate(defaultPickupParticlePrefab, go.transform);
                    ps.Play();
                }

                if (recipe.BuildCompleteClip != null)
                {
                    yield return Announce(recipe.BuildCompleteClip);
                }
                else
                {
                    yield return Announce(buildCompleteClips[Random.Range(0, buildCompleteClips.Length)]);
                }
            }
            else
            {
                Debug.LogError("TODO: handle building recipes of type: " + recipe.GetType().Name);
            }

            recipe.BuildFinished();

            isBuilding = false;
            timeOfNextBuiild = Time.timeSinceLevelLoad + (buildingCooldown * difficultyModifier.Evaluate(FpsSettings.playstyle.difficulty));
        }

        /// <summary>
        /// Adds the recipe to the list of recipes available to these nanobots on this run.
        /// </summary>
        /// <param name="recipe">The recipe to add.</param>
        public void AddToRunRecipes(IRecipe recipe)
        {
#if UNITY_EDITOR
            if (recipe == null)
            {
                Debug.LogError("Attempting to add a null recipe to the NanobotManager.");
                return;
            }

            if (!RogueLiteManager.runData.Contains(recipe))
            {
                throw new ArgumentException($"Attempted to add a recipe ({recipe} - {recipe.DisplayName}) to the current RunRecipes that is not in the `RogueLiteManager.runData`. Should add their first with RogueLiteManager.Add(recipe).");
            }
#endif

            // TODO: This is messy, far too many if...else statements. Do we really need to keep separate lists now that they have a common AbstractRecipe base class?
            if (recipe is AmmoRecipe ammo && !ammoRecipes.Contains(ammo))
            {
                ammoRecipes.Add(ammo);
            }
            else if(recipe is ArmourRecipe armour && !armourRecipes.Contains(armour))
            {
                armourRecipes.Add(armour);
            }
            else if (recipe is HealthPickupRecipe health && !healthRecipes.Contains(health))
            {
                healthRecipes.Add(health);
            } 
            else if (recipe is WeaponRecipe weapon && !weaponRecipes.Contains(weapon))
            {
                weaponRecipes.Add(weapon);
                if (weapon.ammoRecipe != null)
                {
                    AddToRunRecipes(weapon.ammoRecipe);
                }
            }
            else if (recipe is ToolRecipe tool && !toolRecipes.Contains(tool))
            {
                toolRecipes.Add(tool);
            }
            else if (recipe is ShieldRecipe shield && !shieldRecipes.Contains(shield))
            {
                shieldRecipes.Add(shield);
            }
            else if (recipe is ItemRecipe item && !itemRecipes.Contains(item))
            {
                itemRecipes.Add(item);
            } 
            else if (recipe is BaseStatRecipe statRecipe)
            {
                StartCoroutine(ApplyStatModifier(statRecipe));
            }
            else if (recipe is AmmunitionEffectUpgradeRecipe ammoUpgradeRecipe)
            {
                ammoUpgradeRecipe.Apply();
                ammoUpgradeRecipes.Add(ammoUpgradeRecipe);
            }
            else if (recipe is PassiveItemRecipe passiveRecipe)
            {
                if (passiveRecipe.Apply(this))
                {
                    passiveRecipes.Add(passiveRecipe);
                }
            }
        }

        private IEnumerator ApplyStatModifier(BaseStatRecipe statRecipe)
        {
            statRecipe.Apply();

            while (statRecipe.isRepeating && statRecipe.repeatEvery > 0)
            {
                yield return new WaitForSeconds(statRecipe.repeatEvery);
                statRecipe.Apply();
            }
        }

        public static bool IsDerivedFromGenericClass(Type derivedType, Type genericClass)
        {
            while (derivedType != null && derivedType != typeof(object))
            {
                var cur = derivedType.IsGenericType ? derivedType.GetGenericTypeDefinition() : derivedType;
                if (genericClass == cur)
                {
                    return true;
                }
                derivedType = derivedType.BaseType;
            }
            return false;
        }

        public void CollectResources(int amount, ResourceType resourceType)
        {
            resourcesForNextNanobotLevel -= amount;
            m_Resources.Add(amount);

            // TODO: This should be handled by the GameStat.SetValue() method where we collect other stats like this. Or perhaps it should be an event fired here....
            if (resourceType == ResourceType.Crystal)
            {
                m_CrystalResourcesCollected.Add(amount);
            }
        }

        internal int GetAppliedCount(PassiveItemRecipe passiveItemPickupRecipe)
        {
            return passiveRecipes.FindAll(x => x == passiveItemPickupRecipe).Count;
        }

#if UNITY_EDITOR
        [Button]
        private void Add10000Resources()
        {
            m_Resources.Add(10000);
        }
#endif
    }
}