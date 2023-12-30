﻿using UnityEngine;
using NeoFPS.SinglePlayer;
using NeoSaveGames.Serialization;
using NeoSaveGames;
using NeoFPS;
using UnityEngine.Events;
using NeoSaveGames.SceneManagement;
using System.Collections;
using NeoFPS.Constants;

namespace Playground
{
    public class PlaygroundDecember23GameMode : FpsSoloGameCustomisable, ISpawnZoneSelector, ILoadoutBuilder
    {
        [Header("Character")]
        [SerializeField, NeoPrefabField(required = true), Tooltip("The player prefab to instantiate if none exists.")]
        private FpsSoloPlayerController m_PlayerPrefab = null;
        [SerializeField, NeoPrefabField(required = true), Tooltip("The character prefab to use.")]
        private FpsSoloCharacter m_CharacterPrefab = null;
        
        [Header("Level Management")]
        [SerializeField, Tooltip("The level definitions which define the enemies, geometry and more for each level.")]
        LevelDefinition[] levels;
        [SerializeField, Tooltip("The prefab to use when generating level up rewards.")]
        private RecipeSelectorUI rewardsPrefab;

        [Header("Victory")]
        [SerializeField, Tooltip("The amount of time to wait after victory before heading to the hub")]
        float m_VictoryDuration = 5f;

        LevelGenerator levelGenerator;
        int nextRewardsLevel = 200;
        private int spawnersRemaining = int.MaxValue;

        public LevelDefinition currentLevelDefinition
        {
            get { 
                if (levels.Length <= RogueLiteManager.runData.currentLevel)
                    return levels[levels.Length - 1]; 
                else
                    return levels[RogueLiteManager.runData.currentLevel];
            }
        }

        #region Unity Life-cycle
        protected override void Awake()
        {
            levelGenerator = GetComponentInChildren<LevelGenerator>();
            base.Awake();
        }

        private void Update()
        {
            if (FpsSoloCharacter.localPlayerCharacter == null) 
            {
                return;
            }

            if (spawnersRemaining == 0 && m_VictoryCoroutine == null)
            {
                m_VictoryCoroutine = StartCoroutine(DelayedVictoryCoroutine(m_VictoryDuration));
            }

            if (RogueLiteManager.persistentData.currentResources > nextRewardsLevel)
            {
                Transform player = FpsSoloCharacter.localPlayerCharacter.transform;
                Vector3 position = player.position + player.forward * 5;
                RecipeSelectorUI rewards = Instantiate(rewardsPrefab, position, Quaternion.identity);

                nextRewardsLevel = GetRequiredResourcesForNextLevel();
            }
        }
        #endregion

        #region Game Events

        private Coroutine m_VictoryCoroutine = null;
        private float m_VictoryTimer = 0f;

        public static event UnityAction onVictory;

        internal void SpawnerDestroyed()
        {
            spawnersRemaining--;
        }

        protected override void DelayedDeathAction()
        {
            RogueLiteManager.ResetRunData();

            NeoSceneManager.LoadScene(RogueLiteManager.hubScene);
        }

        void DelayedVictoryAction()
        {
            RogueLiteManager.runData.currentLevel++;

            NeoSceneManager.LoadScene(RogueLiteManager.hubScene);
        }

        private IEnumerator DelayedVictoryCoroutine(float delay)
        {
            onVictory?.Invoke();

            yield return null;

            // Delay timer
            m_VictoryTimer = delay;
            while (m_VictoryTimer > 0f && !SkipDelayedDeathReaction())
            {
                m_VictoryTimer -= Time.deltaTime;
                yield return null;
            }

            if (inGame)
                DelayedVictoryAction();
        }

        #endregion

        #region ISpawnZoneSelector IMPLEMENTATION

        [Header("Spawning")]

        [SerializeField, Tooltip("The spawn zones (groups of spawn points) available on this map.")]
        private SpawnZoneSelectorData m_SpawnZones = new SpawnZoneSelectorData();

        private static readonly NeoSerializationKey k_SpawnZoneIndexKey = new NeoSerializationKey("spawnIndex");

        public Sprite mapSprite
        {
            get { return m_SpawnZones.mapSprite; }
        }

        public int numSpawnZones
        {
            get { return m_SpawnZones.spawnZones.Length; }
        }

        public int currentSpawnZoneIndex
        {
            get { return m_SpawnZones.currentIndex; }
            set { m_SpawnZones.currentIndex = value; }
        }

        public ISpawnZoneInfo GetSpawnZoneInfo(int index)
        {
            return m_SpawnZones.spawnZones[index];
        }

        protected override void OnStart()
        {
            base.OnStart();

            if (m_SpawnZones.currentIndex == -1 && m_SpawnZones.spawnZones.Length > 0)
                m_SpawnZones.currentIndex = 0;
        }

        #endregion

        #region ILoadoutBuilder IMPLEMENTATION

        [Header("Loadout Builder")]

        [SerializeField, Tooltip("The loadouts that are available to use.")]
        private LoadoutBuilderData m_LoadoutBuilder = new LoadoutBuilderData();
        
        public int numLoadoutBuilderSlots
        {
            get { return m_LoadoutBuilder.slots.Length; }
        }

        public ILoadoutBuilderSlot GetLoadoutBuilderSlotInfo(int index)
        {
            return m_LoadoutBuilder.slots[index];
        }

        public FpsInventoryLoadout GetLoadout()
        {
            return m_LoadoutBuilder.GetLoadout();
        }

        public void AddToLoadout(FpsInventoryItemBase item)
        {
            RogueLiteManager.runData.AddToLoadout(item);
        }

        protected override void OnCharacterSpawned(ICharacter character)
        {
            var loadout = m_LoadoutBuilder.GetLoadout();
            if (loadout != null)
                character.GetComponent<IInventory>()?.ApplyLoadout(loadout);

            // Add nanobot recipes
            NanobotManager manager = character.GetComponent<NanobotManager>();
            for (int i = 0; i < RogueLiteManager.runData.Recipes.Count; i++)
            {
                manager.Add(RogueLiteManager.runData.Recipes[i]);
            }

            for (int i = 0; i < RogueLiteManager.persistentData.RecipeIds.Count; i++)
            {
                if (RecipeManager.TryGetRecipeFor(RogueLiteManager.persistentData.RecipeIds[i], out IRecipe recipe))
                {
                    manager.Add(recipe);

                    WeaponPickupRecipe weaponRecipe = recipe as WeaponPickupRecipe;
                    if (weaponRecipe != null)
                    {
                        if (weaponRecipe.ammoRecipe != null)
                        {
                            manager.Add(weaponRecipe.ammoRecipe);
                        }
                    }
                }
            }

            nextRewardsLevel = GetRequiredResourcesForNextLevel();
        }

        private static int GetRequiredResourcesForNextLevel()
        {
            int level = RogueLiteManager.runData.currentLevel + 1;
            return RogueLiteManager.persistentData.currentResources + (level * level * 100);
        }

        #endregion

        protected override bool PreSpawnStep()
        {
            RogueLiteManager.persistentData.runNumber++;

            if (currentLevelDefinition.generateLevelOnSpawn)
            {
                spawnersRemaining = levelGenerator.Generate(this);
            }

            for (int i = 0; i < RogueLiteManager.persistentData.RecipeIds.Count; i++)
            {
                if (RecipeManager.TryGetRecipeFor(RogueLiteManager.persistentData.RecipeIds[i], out IRecipe recipe) == false)
                {
                    continue;
                }

                RogueLiteManager.runData.Add(recipe);

                WeaponPickupRecipe weaponRecipe = recipe as WeaponPickupRecipe;
                if (weaponRecipe != null)
                {
                    if (weaponRecipe.pickup == null)
                    {
                        Debug.LogError("WeaponPickupRecipe " + weaponRecipe.name + " has no pickup assigned.");
                    }
                    RogueLiteManager.runData.AddToLoadout(weaponRecipe.pickup.GetItemPrefab());
                }

                ToolPickupRecipe toolRecipe = recipe as ToolPickupRecipe;
                if (toolRecipe != null)
                {
                    if (toolRecipe.pickup == null)
                    {
                        Debug.LogError("ToolPickupRecipe " + toolRecipe.name + " has no pickup assigned.");
                        break;
                    }
                    RogueLiteManager.runData.AddToLoadout(toolRecipe.pickup.GetItemPrefab());
                }
            }

            for (int i = 0; i < RogueLiteManager.runData.Loadout.Count; i++)
            {
                FpsInventoryItemBase item = RogueLiteManager.runData.Loadout[i] as FpsInventoryItemBase;
                FpsSwappableCategory category = FpsSwappableCategory.Firearm;

                FpsInventoryQuickUseSwappableItem quickUse = item as FpsInventoryQuickUseSwappableItem;
                if (quickUse != null)
                {
                    category = quickUse.category; 
                }
                else
                {
                    FpsInventoryWieldableSwappable wieldable = item as FpsInventoryWieldableSwappable;
                    if (wieldable != null)
                    {
                        category = wieldable.category;
                    }
                }
                m_LoadoutBuilder.slots[category].AddOption(item);
            }

            RogueLiteManager.persistentData.isDirty = true;
            RogueLiteManager.runData.isDirty = true;

            return base.PreSpawnStep();
        }

        protected override IController GetPlayerControllerPrototype()
        {
            return m_PlayerPrefab;
        }

        protected override ICharacter GetPlayerCharacterPrototype(IController player)
        {
            return m_CharacterPrefab;
        }

        public override void WriteProperties(INeoSerializer writer, NeoSerializedGameObject nsgo, SaveMode saveMode)
        {
            base.WriteProperties(writer, nsgo, saveMode);

            writer.WriteValue(k_SpawnZoneIndexKey, m_SpawnZones.currentIndex);
        }

        public override void ReadProperties(INeoDeserializer reader, NeoSerializedGameObject nsgo)
        {
            base.ReadProperties(reader, nsgo);

            int index;
            if (reader.TryReadValue(k_SpawnZoneIndexKey, out index, m_SpawnZones.currentIndex))
                m_SpawnZones.currentIndex = index;
        }
    }
}
