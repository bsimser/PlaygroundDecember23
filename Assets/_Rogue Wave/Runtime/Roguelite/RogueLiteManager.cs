using NaughtyAttributes;
using NeoFPS;
using RogueWave.GameStats;
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace RogueWave
{
    [CreateAssetMenu(fileName = "FpsManager_RogueLite", menuName = "Rogue Wave/Rogue-Lite Manager", order = 900)]
    public class RogueLiteManager : NeoFpsManager<RogueLiteManager>
    {
        [Header("Scenes")]
        [SerializeField, Tooltip("Name of the Main Menu Scene to load. This is where the player starts the game."), Scene]
        private string m_mainMenuScene = "RogueWave_MainMenu";
        [SerializeField, Tooltip("Name of the Hub Scene to load between levels. This is where the player gets to buy permanent upgrades for their character."), Scene]
        private string m_reconstructionScene = "RogueWave_ReconstructionScene";
        [SerializeField, Tooltip("Name of the Reconstruction Scene to load upon death. This will show a summary of the players most recent run."), Scene]
        private string m_hubScene = "RogueWave_HubScene";
        [SerializeField, Tooltip("The scene to load when the player enters the portal."), Scene]
        private string m_portalScene = "RogueWave_PortalUsed";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            currentProfile = null;
        }

        public static string reconstructionScene
        {
            get { return instance.m_reconstructionScene; }
        }

        static string ProfilesFolderPath {
            get { return string.Format("{0}\\{1}\\", Application.persistentDataPath, k_Subfolder); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadRogueLiteManager()
        {
            UpdateAvailableProfiles();
            GetInstance("FpsManager_RogueLite");
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Rogue Wave/Profiles/Explore To Profiles Folder", priority = 0)]
        static void ExploreToProfilesFolder()
        {
            Application.OpenURL(ProfilesFolderPath);
        }

        [UnityEditor.MenuItem("Tools/Rogue Wave/Profiles/Delete Profiles", priority = 1)]
        static void DeleteProfiles()
        {
            DirectoryInfo directory = new DirectoryInfo(ProfilesFolderPath);
            if (directory.Exists)
            {
                directory.Delete(true);
                UpdateAvailableProfiles();
            }
        }
#endif

        const string k_ProfileExtension = "profileData";
        const string k_StatsExtension = "statsData";
        const string k_Subfolder = "Profiles";

        private RuntimeBehaviour m_ProxyBehaviour = null;

        public static FileInfo[] availableProfiles
        {
            get;
            private set;
        } = { };

        public static string currentProfile
        {
            get;
            private set;
        } = string.Empty;

        public static string mainMenuScene
        {
            get
            {
                if (instance != null)
                    return instance.m_mainMenuScene;
                else
                    return string.Empty;
            }
        }

        public static string hubScene
        {
            get
            {
                if (instance != null)
                    return instance.m_hubScene;
                else
                    return string.Empty;
            }
        }

        public static string combatScene
        {
            get
            {
                return "RogueWave_CombatLevel";
            }
        }

        public static string portalScene
        {
            get
            {
                if (instance != null)
                    return instance.m_portalScene;
                else
                    return string.Empty;
            }
        }

        private static RogueLitePersistentData m_PersistentData = null;
        public static RogueLitePersistentData persistentData
        {
            get
            {
                if (m_PersistentData == null)
                    ResetPersistentData();
                return m_PersistentData;
            }
        }

        private static RogueLiteRunData m_RunData = null;
        public static RogueLiteRunData runData
        {
            get
            {
                if (m_RunData == null)
                    ResetRunData();
                return m_RunData;
            }
        }

        public static bool hasProfile { 
            get
            {
                return availableProfiles != null && availableProfiles.Length != 0;
            } 
        }

        protected override void OnDestroy()
        {
            RogueLiteManager.persistentData.isDirty = true; // set to true as a security in case we have any bugs not setting it
            SaveProfile();

            base.OnDestroy();
        }

        public static void ResetRunData()
        {
            m_RunData = new RogueLiteRunData();
        }

        public override bool IsValid()
        {
            return true;
        }

        protected override void Initialise()
        {
            m_ProxyBehaviour = GetBehaviourProxy<RuntimeBehaviour>();
        }

        class RuntimeBehaviour : MonoBehaviour
        {
            void Start()
            {
                StartCoroutine(SaveProfileData());
            }
            IEnumerator SaveProfileData()
            {
                var wait = new WaitForSecondsRealtime(3f);
                while (true)
                {
                    yield return wait;

                    SaveProfile();
                }
            }
        }

        static RogueLitePersistentData CreatePersistentDataFromJson(string json)
        {
            if (!string.IsNullOrEmpty(json))
                m_PersistentData = JsonUtility.FromJson<RogueLitePersistentData>(json);
            else
                m_PersistentData = new RogueLitePersistentData();

            return persistentData;
        }

        public static RogueLitePersistentData ResetPersistentData()
        {
            m_PersistentData = new RogueLitePersistentData();
            return persistentData;
        }

        public static void AssignPersistentData(RogueLitePersistentData custom)
        {
            if (custom != null)
                m_PersistentData = custom;
            else
                m_PersistentData = new RogueLitePersistentData();
        }

        internal static void UpdateAvailableProfiles()
        {
            // Get or create the profiles folder
            DirectoryInfo directory = Directory.Exists(ProfilesFolderPath) ? new DirectoryInfo(ProfilesFolderPath) : Directory.CreateDirectory(ProfilesFolderPath);

            // Get and sort an array of profile files with the correct extension
            if (directory != null)
            {
                FileInfo[] result = directory.GetFiles("*." + k_ProfileExtension);
                Array.Sort(result, (FileInfo f1, FileInfo f2) => { return f2.CreationTime.CompareTo(f1.CreationTime); });
                availableProfiles = result;
            }
            else
                availableProfiles = new FileInfo[0];

            if (currentProfile == string.Empty && availableProfiles.Length > 0)
            {
                LoadProfile(0);
            }
        }

        public static void CreateNewProfile(string profileName)
        {
            currentProfile = profileName;
            ResetPersistentData();
            ResetRunData();
            GameStatsManager.Instance.ResetStats();
            persistentData.isDirty = true;
        }

        public static string GetProfileName(int index)
        {
            if (availableProfiles == null || availableProfiles.Length == 0)
                return string.Empty;
            else if (index < 0 || index >= availableProfiles.Length)
                return string.Empty;
            else
                return Path.GetFileNameWithoutExtension(availableProfiles[index].Name);
        }

        public static void LoadProfile(int index)
        {
            RogueLiteManager.persistentData.isDirty = true; // Set to true as a security in case we fogot to set it somewhere
            SaveProfile();

            // Load the file if available and create new instance from json
            using (var stream = availableProfiles[index].OpenText())
            {
                string json = stream.ReadToEnd();
                CreatePersistentDataFromJson(json);
            }

            // Get the profile name
            currentProfile = GetProfileName(index);

            // Load the stats from the saved files
            string path = string.Format("{0}{1}.{2}", ProfilesFolderPath, currentProfile, k_StatsExtension);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                StatsWrapperArray wrapperArray = JsonUtility.FromJson<StatsWrapperArray>(json);
                IntGameStat[] stats = Resources.LoadAll<IntGameStat>("");

                for (int i = 0; i < wrapperArray.stats.Length; i++)
                {
                    for (int y = 0; y < stats.Length; y++)
                    {
                        if (wrapperArray.stats[i].key == stats[y].key)
                        {
                            stats[y].SetValue(wrapperArray.stats[i].value);
                        }
                    }
                }
            }
        }

        public static void SaveProfile()
        {
//#if UNITY_EDITOR
//            if (currentProfile == string.Empty)
//            {
//                currentProfile = "Test";

//                FileInfo newProfile = new FileInfo(string.Format("{0}\\{1}.{2}", Application.persistentDataPath, currentProfile, k_Extension));

//                if (availableProfiles == null)
//                {
//                    availableProfiles = new FileInfo[] { newProfile };
//                }
//                else
//                {
//                    List<FileInfo> temp = new List<FileInfo>(availableProfiles);
//                    temp.Add(newProfile);
//                    availableProfiles = temp.ToArray();
//                }
//            }
//#endif

            // Only save if there have been changes
            if (persistentData == null || !persistentData.isDirty || currentProfile == string.Empty)
                return;

            // Check the folder exists
            if (!Directory.Exists(ProfilesFolderPath))
                Directory.CreateDirectory(ProfilesFolderPath);

            // Write the profile data
            using (var stream = File.CreateText(string.Format("{0}{1}.{2}", ProfilesFolderPath, currentProfile, k_ProfileExtension)))
            {
                string json = JsonUtility.ToJson(m_PersistentData, true);
                stream.Write(json);
            }

            // Write the stats data
            using (var stream = File.CreateText(string.Format("{0}{1}.{2}", ProfilesFolderPath, currentProfile, k_StatsExtension)))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{\n\"stats\": [");
                IntGameStat[] stats = Resources.LoadAll<IntGameStat>("");
                for (int i = 0; i < stats.Length; i++)
                {
                    StatsWrapper wrapper = new StatsWrapper(stats[i].key);
                    wrapper.value = stats[i].value;
                    sb.Append(JsonUtility.ToJson(wrapper, true));
                    if (i < stats.Length - 1)
                    {
                        sb.AppendLine(",");
                    } else
                    {
                        sb.AppendLine();
                    }
                }
                sb.AppendLine("]}");
                stream.Write(sb.ToString());
            }

            // Wipe the instance dirty flag
            persistentData.isDirty = false;

            // Update available saves
            UpdateAvailableProfiles();
        }

        /// <summary>
        /// Get the total Count of a recipe in the player's current recipe permanent + temporary collection.
        /// </summary>
        /// <param name="recipe">The recipe to count.</param>
        /// <returns>Total number of recipes held in current permanent and temporary collections.</returns>
        /// <seealso cref="RogueLiteRunData.GetCount(IRecipe)"/>
        /// <seealso cref="RogueLitePersistentData.GetCount(IRecipe)"/>
        internal static int GetTotalCount(IRecipe recipe)
        {
            int total = runData.GetCount(recipe);
            total += persistentData.GetCount(recipe);
            return total;
        }

        [System.Serializable]
        private class StatsWrapper
        {
            public string key;
            public int value;

            public StatsWrapper(string key)
            {
                this.key = key;
            }
        }

        [Serializable]
        private class StatsWrapperArray
        {
            public StatsWrapper[] stats;
        }
    }
}