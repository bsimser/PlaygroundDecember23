using NaughtyAttributes;
using NeoFPS;
using NeoFPS.SinglePlayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace RogueWave
{
    [RequireComponent(typeof(AudioSource))]
    public class Spawner : MonoBehaviour
    {
        [Header("Spawn Behaviours")]
        [SerializeField, Tooltip("If true then the spawner will use the level definition defined in the Game Mode to determine the waves to spawn. If false then the spawner will spawn according to the wave definition below.")]
        bool useLevelDefinition = false;
        [SerializeField, HideIf("useLevelDefinition"), Tooltip("The level definition to use to determine the waves to spawn. Set 'Use Level Definition' to true to use the waves set in the level.")]
        LevelDefinition levelDefinition;
        [SerializeField, Tooltip("The time to wait between waves.")]
        private float timeBetweenWaves = 5;
        [SerializeField, Tooltip("If true then the spawner will ignore the max alive setting in the level definition and will spawn as many enemies as it can.")]
        private bool ignoreMaxAlive = false;
        [SerializeField, Tooltip("Distance to player for this spawner to be activated. If this is set to 0 then it will always be active, if >0 then the spawner will only be active when the player is within this many units. If the player moves further away then the spawner will pause.")]
        float activeRange = 0;
        [SerializeField, Tooltip("The radius around the spawner to spawn enemies.")]
        internal float spawnRadius = 5f;
        [SerializeField, Tooltip("If true, all enemies spawned by this spawner will be destroyed when this spawner is destroyed.")]
        internal bool destroySpawnsOnDeath = true;
        
        [Header("Shield")]
        [SerializeField, Tooltip("Should this spawner generate a shield to protect itself?")]
        bool hasShield = true;
        [SerializeField, ShowIf("hasShield"), Tooltip("Shield generators are models that will orbit the spawner and create a shield. The player must destroy the generators to destroy the shield and thus get t othe spawner.")]
        internal BasicHealthManager shieldGenerator;
        [SerializeField, ShowIf("hasShield"), Tooltip("How many generators this shield should have.")]
        internal int numShieldGenerators = 3;
        [SerializeField, ShowIf("hasShield"), Tooltip("The speed of the shield generators, in revolutions per minute.")]
        internal float shieldGeneratorRPM = 45f;
        [SerializeField, ShowIf("hasShield"), Tooltip("The model and collider that will represent the shield.")]
        internal Collider shieldCollider;

        [Header("Feel")]
        [SerializeField, Tooltip("The sound to play when a new spawning wave is starting.")]
        AudioClip waveStartSound;

        [Header("Events")]
        [SerializeField, Tooltip("The event to trigger when this spawner is destroyed.")]
        public UnityEvent<Spawner> onDestroyed;
        [SerializeField, Tooltip("The event to trigger when an enemy is spawned.")]
        public UnityEvent<BasicEnemyController> onEnemySpawned;
        [SerializeField, Tooltip("The event to trigger when all waves are complete.")]
        public UnityEvent onAllWavesComplete;

        List<BasicEnemyController> spawnedEnemies = new List<BasicEnemyController>();
        List<ShieldGenerator> shieldGenerators = new List<ShieldGenerator>();

        private class ShieldGenerator
        {
            public BasicHealthManager healthManager;
            public GameObject gameObject;
            public Transform transform;

            public ShieldGenerator (BasicHealthManager h)
            {
                healthManager = h;
                gameObject = h.gameObject;
                transform = h.transform;
            }

            public void Respawn(float health)
            {
                healthManager.healthMax = health;
                healthManager.health = health;
                // TODO: Shader
            }
        }

        private LevelDefinition currentLevel;
        private int currentWaveIndex = -1;
        private WaveDefinition currentWave;


        private int livingShieldGenerators = 0;
        private float activeRangeSqr;
        private AudioSource audioSource;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (hasShield == false)
            {
                return;
            }

            var rotation = new Vector3(0f, shieldGeneratorRPM * Time.deltaTime, 0f);
            for (int i = 0; i < shieldGenerators.Count; i++)
            {
                if (shieldGenerators[i].gameObject) {
                    shieldGenerators[i].transform.Rotate(rotation, Space.Self);
                } else
                {
                    shieldGenerators.RemoveAt(i);
                }

            }
        }

        private void Start()
        {
            transform.localScale = new Vector3(spawnRadius * 2, spawnRadius * 2, spawnRadius * 2);

            if (useLevelDefinition == false)
            {
                if (levelDefinition == null)
                {
                    Debug.LogError($"{this} is set to use the level definition to define spawning waves. However, no Level Definition is available. The spawner will not be active.");
                    return;
                }
                currentLevel = levelDefinition;
            }

            StartCoroutine(SpawnWaves());

            if (hasShield && shieldGenerator != null)
            {
                RegisterShieldGenerator(shieldGenerator);
                for (int i = 1; i < numShieldGenerators; ++i)
                {
                    var duplicate = Instantiate(shieldGenerator, shieldGenerator.transform.parent);
                    duplicate.transform.localRotation = Quaternion.Euler(0f, 360f * i / numShieldGenerators, 0f);
                    RegisterShieldGenerator(duplicate);
                }

                livingShieldGenerators = numShieldGenerators;
            }

            activeRangeSqr = activeRange * activeRange;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();

            onDestroyed?.Invoke(this);
        }

        void RegisterShieldGenerator(BasicHealthManager h)
        {
            var sg = new ShieldGenerator(h);
            shieldGenerators.Add(sg);
            h.onIsAliveChanged += OnShieldGeneratorIsAliveChanged;
        }

        private void OnShieldGeneratorIsAliveChanged(bool alive)
        {
            int oldLivingShieldGenerators = livingShieldGenerators;

            if (alive)
            {
                ++livingShieldGenerators;
            }
            else
            {
                --livingShieldGenerators;
            }

            if (shieldCollider != null)
            {
                if (livingShieldGenerators == 0 && oldLivingShieldGenerators != 0)
                {
                    // Disable the shield
                    shieldCollider.enabled = false;
                    shieldCollider.gameObject.SetActive(false); // TODO: Shader
                }
                else
                {
                    if (livingShieldGenerators != 0 && oldLivingShieldGenerators == 0)
                    {
                        // Enable the shield
                        shieldCollider.enabled = true;
                        shieldCollider.gameObject.SetActive(true); // TODO: Shader
                    }
                }
            }
        }

        private void NextWave()
        {
            currentWaveIndex++;
            if (currentWaveIndex >= currentLevel.waves.Length)
            {
                onAllWavesComplete?.Invoke();
                if (!currentLevel.generateNewWaves)
                {
                    Debug.LogWarning("No more waves to spawn.");
                    currentWave = null;
                    StopCoroutine(SpawnWaves());
                    return;
                }
                currentWave = GenerateNewWave();
                return;
            }
            //Debug.Log($"Starting wave {currentWaveIndex + 1} of {waves.Length}...");
            currentWave = currentLevel.waves[currentWaveIndex];
            currentWave.Reset();

            if (waveStartSound != null)
            {
                audioSource.clip = waveStartSound;
                audioSource.Play();
            }
        }

        private WaveDefinition GenerateNewWave()
        {
            WaveDefinition newWave = ScriptableObject.CreateInstance<WaveDefinition>();
            // populate newWave with random values based loosely on the previous wave
            var lastWave = currentWave;
            if (lastWave == null)
            {
                lastWave = currentLevel.waves[currentLevel.waves.Length - 1];
            }
            // collect all enemy prefabs from all waves and then randomly select from them
            List<BasicEnemyController> enemyPrefabs = new List<BasicEnemyController>();
            for (int i = 0; i < currentLevel.waves.Length; i++)
            {
                enemyPrefabs.AddRange(currentLevel.waves[i].EnemyPrefabs);
            }
            newWave.Init(
                enemyPrefabs.ToArray(),
                Mathf.Max(lastWave.SpawnRate - 0.1f, 0.1f), // faster!
                lastWave.WaveDuration + Random.Range(1f, 5f), // longer!
                WaveDefinition.SpawnOrder.Random
            );
            return newWave;
        }

        private IEnumerator SpawnWaves()
        {
            while (FpsSoloCharacter.localPlayerCharacter == null || currentLevel == null)
            {
                yield return new WaitForSeconds(0.5f);
            }


            NextWave();
            while (currentWave != null)
            {
                float waveStart = Time.time;
                WaitForSeconds waitForSpawnRate = new WaitForSeconds(currentWave.SpawnRate);
                while (Time.time - waveStart < currentWave.WaveDuration && (ignoreMaxAlive == false || currentLevel.maxAlive == 0 || spawnedEnemies.Count < currentLevel.maxAlive))
                {
                    yield return waitForSpawnRate;

                    for (int i = 0; i < currentWave.SpawnAmount; i++)
                    {
                        if (activeRange == 0 || Vector3.SqrMagnitude(FpsSoloCharacter.localPlayerCharacter.transform.position - transform.position) <= activeRangeSqr)
                        {
                            SpawnEnemy();
                            while (ignoreMaxAlive == false && currentLevel.maxAlive != 0 && spawnedEnemies.Count >= currentLevel.maxAlive) {
                                yield return waitForSpawnRate;
                            }
                            yield return null;
                        }
                    }
                }

                yield return new WaitForSeconds(timeBetweenWaves);
                NextWave();
            }
        }

        private void SpawnEnemy()
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            var prefab = currentWave.GetNextEnemy();
            if (prefab == null)
            {
                Debug.LogError("No enemy prefab found in wave definition.");
                return;
            }
            BasicEnemyController enemy = Instantiate(prefab, spawnPosition, Quaternion.identity);
            enemy.onDeath.AddListener(OnEnemyDeath);
            
            spawnedEnemies.Add(enemy);
            onEnemySpawned?.Invoke(enemy);
        }

        private void OnEnemyDeath(BasicEnemyController enemy)
        {
            spawnedEnemies.Remove(enemy);
        }

        public void OnAliveIsChanged(bool isAlive)
        {
            if (isAlive)
            {
                StartCoroutine(SpawnWaves());
            }
            else
            {
                if (destroySpawnsOnDeath)
                {
                    for (int i = 0; i < spawnedEnemies.Count; i++)
                    {
                        if (spawnedEnemies[i] != null)
                        {
                            spawnedEnemies[i].OnAliveIsChanged(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Configure this spawner accoring to a level definition.
        /// </summary>
        /// <param name="currentLevelDefinition"></param>
        internal void Initialize(LevelDefinition currentLevelDefinition)
        {
            currentLevel = currentLevelDefinition;
        }

        /// <summary>
        /// The spawner will attempt to spawn the given scanner prefab.
        /// </summary>
        /// <param name="scannerPrefab"></param>
        internal void RequestSpawn(BasicEnemyController enemyPrefab)
        {
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
            BasicEnemyController enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            spawnedEnemies.Add(enemy);

            onEnemySpawned?.Invoke(enemy);
        }
    }
}