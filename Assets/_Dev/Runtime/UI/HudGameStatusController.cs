﻿using NeoFPS;
using RogueWave.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RogueWave
{
	public class HudGameStatusController : PlayerCharacterHudBase
    {
        [SerializeField, Tooltip("The resources UI section. This will be shown and hidden at appropriate times.")]
        private RectTransform m_ResourcesUI = null;
		[SerializeField, Tooltip("The text readout for the current characters resources.")]
		private Text m_ResourcesText = null;
        [SerializeField, Tooltip("The text readout for the number of remaining spawners.")]
        private Text m_SpawnersText = null;
        [SerializeField, Tooltip("The text readout for the number of remaining enemies.")]
        private Text m_EnemiesText = null;
        [SerializeField, Tooltip("The text readout for the current game level number.")]
        private TMPro.TMP_Text m_GameLevelNumberText = null;
        [SerializeField, Tooltip("The text readout for the current players Nanobot level number.")]
        private TMPro.TMP_Text m_NanobotLevelNumberText = null;

        private RogueWaveGameMode gameMode = null;
        private NanobotManager nanobotManager = null;

        int spawnersCount = 0;
        private int enemiesCount;

        protected override void Awake()
        {
            base.Awake();

            gameMode = FindObjectOfType<RogueWaveGameMode>();
            if (gameMode != null)
            {
                gameMode.onSpawnerCreated.AddListener(OnSpawnerCreated);
            }
        }

        private void OnDisable()
        {
            gameMode.onSpawnerCreated.RemoveListener(OnSpawnerCreated);
        }

        private void OnSpawnerCreated(Spawner spawner)
        {
            if (spawner.isBossSpawner)
            {
                spawnersCount++;
            }

            if (m_SpawnersText != null)
            {
                m_SpawnersText.text = spawnersCount.ToString();
            }

            spawner.onSpawnerDestroyed.AddListener(OnSpawnerDestroyed);
            spawner.onEnemySpawned.AddListener(OnEnemySpawned);
        }

        private void OnSpawnerDestroyed(Spawner spawner)
        {
            if (spawner.isBossSpawner)
            {
                spawnersCount--;
            }

            if (m_SpawnersText != null)
            {
                m_SpawnersText.text = spawnersCount.ToString();
            }
        }

        private void OnEnemySpawned(BasicEnemyController enemy)
        {
            enemiesCount++;
            if (m_EnemiesText != null)
            {
                m_EnemiesText.text = enemiesCount.ToString();
            }

            enemy.onDestroyed.AddListener(OnEnemyDestroyed);
        }

        private void OnEnemyDestroyed()
        {
            enemiesCount--;
            if (m_EnemiesText != null)
            {
                m_EnemiesText.text = enemiesCount.ToString();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (nanobotManager != null)
                nanobotManager.onResourcesChanged -= OnResourcesChanged;
        }

        public override void OnPlayerCharacterChanged(ICharacter character)
        {
            if (nanobotManager != null)
            {
                nanobotManager.onResourcesChanged -= OnResourcesChanged;
            }

            if (character as Component != null)
            {
                nanobotManager = character.GetComponent<NanobotManager>();
            }
            else
            {
                nanobotManager = null;
            }

            if (nanobotManager != null)
            {
                nanobotManager.onNanobotLevelUp += OnNanobotLevelUp;
                OnNanobotLevelUp(RogueLiteManager.persistentData.currentNanobotLevel, 150);

                nanobotManager.onResourcesChanged += OnResourcesChanged;
                OnResourcesChanged(0f, nanobotManager.resources, nanobotManager.resources);
                
                m_ResourcesUI.gameObject.SetActive(true);
            }
            else
            {
                m_ResourcesUI.gameObject.SetActive(false);
            }

            if (m_GameLevelNumberText != null)
            {
                m_GameLevelNumberText.text = (RogueLiteManager.persistentData.currentGameLevel + 1).ToString();
            }
        }

        protected void OnNanobotLevelUp(int level, int resourcesForNextLevel)
        {
            if (m_NanobotLevelNumberText != null)
            {
                m_NanobotLevelNumberText.text = (level + 1).ToString();
            }
        }

		protected virtual void OnResourcesChanged (float from, float to, float resourcesUntilNextLevel)
        {
            m_ResourcesText.text = ((int)to).ToString ();
        }
    }
}