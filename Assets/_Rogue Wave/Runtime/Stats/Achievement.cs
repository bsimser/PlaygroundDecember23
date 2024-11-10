using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;
using WizardsCode.RogueWave;

namespace RogueWave.GameStats
{
    [CreateAssetMenu(fileName = "New Achievement", menuName = "Rogue Wave/Stats/Achievement")]
    public class Achievement : ScriptableObject, IParameterizedGameEventListener<int>
    {
        public enum Category { Uncategorized, Levelling, Offense, Defense, Objectives }

        [SerializeField, Tooltip("A demo locked achievement is one that is not available to the demo version of the game. Demo locked achievements may not have been implemented yet.")]
        bool m_IsDemoLocked = true;
        [SerializeField, Tooltip("The key to use to store this achievement in the GameStatsManager.")]
        string m_Key;
        [SerializeField, Tooltip("The name of the achievement as used in the User Interface."), FormerlySerializedAs("m_DispayName")]
        string m_DisplayName;
        [SerializeField, Tooltip("The description of the achievement as used in the User Interface.")]
        string m_Description;
        [SerializeField, Tooltip("The hero image for the achievement.")]
        Sprite m_HeroImage;
        [SerializeField, Tooltip("The icon to use for the achievement.")]
        Sprite m_Icon;
        [SerializeField, Tooltip("The category of the achievement.")]
        Category m_Category = Category.Uncategorized;

        [Header("Tracking")]
        [SerializeField, Tooltip("The stat that this achievement is tracking.")]
        IntGameStat m_StatToTrack;
        [SerializeField, Tooltip("The value that the stat must reach for the achievement to be unlocked.")]
        float m_TargetValue;

        [Header("Events")]
        [SerializeField, Tooltip("The event to raise when this achievement is unlocked.")]
        internal AchievementUnlockedEvent onUnlockEvent = default;

        [SerializeField, Tooltip("Is this achievement unlocked (as in has the player completed the achievement."), ReadOnly]
        bool m_IsUnlocked = false;
        [SerializeField, Tooltip("The UTC time the achievement was unlocked (if it is unlocked)."), ReadOnly, ShowIf("m_IsUnlocked")]
        string m_TimeOfUnlock;

        public bool isDemoLocked => m_IsDemoLocked;
        public string key => m_Key;
        public string displayName => m_DisplayName;
        public string description => m_Description;
        public Sprite icon => m_Icon;
        public Category category
        {
            get => m_Category;
            set => m_Category = value;
        }
        public IntGameStat stat => m_StatToTrack;
        public float targetValue => m_TargetValue;
        public bool isUnlocked => m_IsUnlocked;
        public string timeOfUnlock => m_TimeOfUnlock;
        
        private void OnEnable()
        {
#if UNITY_EDITOR
            if (stat == null)
            {
                Debug.LogError($"Achievement {displayName} has no stat to track.");
            }
#endif
            stat.onChangeEvent?.AddListener(this);
        }

        private void OnDisable()
        {
            stat.onChangeEvent?.RemoveListener(this);
        }

        internal void Reset()
        {
            m_IsUnlocked = false;
        }

        internal void Unlock() 
        {
            if (isUnlocked) return;
            
            m_IsUnlocked = true;
            m_TimeOfUnlock = DateTime.UtcNow.ToString();
            onUnlockEvent?.Raise(this);
            GameLog.Info($"Achievement {displayName} unlocked!");
        }

        public void OnEventRaised(IParameterizedGameEvent<int> e, int change)
        {
            if (e is IntStatEvent intEvent && intEvent.stat == m_StatToTrack)
            {
                if (intEvent.stat.value >= m_TargetValue)
                {
                    Unlock();
                }
            }
        }

#if UNITY_EDITOR
        [Button]
        void TestUnlock()
        {
            Unlock();
        }

        [Button]
        void TestReset()
        {
            Reset();
        }

        [Button("Set to demo locked (recommended as achievement is not valid)"), HideIf(EConditionOperator.Or, "Validate", "m_IsDemoLocked")]
        void SetToDemoLocked()
        {
            m_IsDemoLocked = true;
        }

        internal bool Validate()
        {
            return Validate(out string _);
        }

        internal bool Validate(out string message)
        {
            return IsValid(out message);
        }

        bool IsValid(out string message)
        {
            if (string.IsNullOrEmpty(m_Key))
            {
                message = "Key cannot be empty";
                return false;
            }

            if (!IsValidName(out message))
            {
                return false;
            }

            if (m_StatToTrack == null)
            {
                message = "Stat to track cannot be empty";
                return false;
            }

            if (m_Category == Category.Uncategorized)
            {
                message = "Category cannot be Uncategorized";
                return false;
            }

            string path = UnityEditor.AssetDatabase.GetAssetPath(this);
            string[] pathParts = path.Split('/');
            if (pathParts.Length < 2 || pathParts[pathParts.Length - 2] != m_Category.ToString())
            {
                message = $"Category and storage folder for {name} do not match.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        bool IsValidName(out string message)
        {
            if(string.IsNullOrEmpty(m_DisplayName))
            {
                message = "Display Name cannot be empty";
                return false;
            }

            if (m_DisplayName.Length > 50)
            {
                message = "Display Name cannot be longer than 50 characters";
                return false;
            }

            if (m_DisplayName.Contains("\n"))
            {
                message = "Display Name cannot contain new lines";
                return false;
            }

            if (m_DisplayName.Contains("\r"))
            {
                message = "Display Name cannot contain carriage returns";
                return false;
            }

            if (m_DisplayName != this.name)
            {
                message = $"Dispaly name '{m_DisplayName}' is not the same as the filename '{name}'.";
                return false;
            }

            message = string.Empty;
            return true;
        }
#endif
    }
}