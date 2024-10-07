using Codice.Utils;
using NaughtyAttributes;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace WizardsCode.Marketing
{
    public class AssetDescriptor : ScriptableObject
    {
        [Header("Metadata")]
        [SerializeField, Tooltip("A name to be used in filenames and other references."), FormerlySerializedAs("assetName")]
        string m_AssetName;
        [SerializeField, Tooltip("The description of the asset, used as a reminder for why this is needed."), TextArea(10,25), FormerlySerializedAs("description")]
        string m_Description;

        [Header("Asset Settings")]
        [SerializeField, Tooltip("The resolution of the asset in pixels.")]
        Vector2Int m_Resolution = new Vector2Int(1920, 1080);
        [SerializeField, Tooltip("The target frame rate for the asset. If the asset is only a helo image then this is the frame rate of the rendering of frames around the hero frame.")]
        float m_FrameRate = 60;
        [SerializeField, Tooltip("The hero frame is the one that is most pleasing within the sequence. This frame, and one either side, will always be captured as a still, regardless of the kind of asset being generated.")]
        int m_HeroFrame = 30;
        [SerializeField, Tooltip("How many frames to capture before and after the hero frame.")]
        int m_FramesEitherSideOfHero = 3;
        [SerializeField, Tooltip("The time, in game time, when the asset recording starts. You should leave enough frames for the scene to \"warm up\".")]
        float m_StartTime = 0;
        [SerializeField, Tooltip("The time, in game time, when the asset recording stops..")]
        float m_EndTime = 2;

        [SerializeField, Tooltip("The camera position for the asset capture."), Foldout("Scene Setup")]
        Vector3 m_CameraPosition = Vector3.zero;
        [SerializeField, Tooltip("The camera rotation for the asset capture."), Foldout("Scene Setup")]
        Vector3 m_CameraRotation;
        [SerializeField, Tooltip("The logo enabled state for the asset capture."), Foldout("Scene Setup")]
        bool m_LogoEnabled = true;
        [SerializeField, Tooltip("The logo position for the asset capture."), Foldout("Scene Setup"), ShowIf("m_LogoEnabled")]
        Vector3 m_LogoPosition;
        [SerializeField, Tooltip("The logo rotation for the asset capture."), Foldout("Scene Setup"), ShowIf("m_LogoEnabled")]
        Vector3 m_LogoRotation;
        [SerializeField, Tooltip("The fog enabled state for the asset capture."), Foldout("Scene Setup")]
        private bool m_FogEnabled = true;
        [SerializeField, Tooltip("The posisition of the background during this asset capture."), Foldout("Scene Setup")]
        Vector3 m_BackgroundPosition;
        [SerializeField, Tooltip("The rotation of the background during this asset capture."), Foldout("Scene Setup")]
        Vector3 m_BackgroundRotation;

        internal string AssetName => m_AssetName;
        internal string Description => m_Description;
        internal Vector2Int Resolution => m_Resolution;
        internal float FrameRate => m_FrameRate;
        internal int HeroFrame => m_HeroFrame;
        internal float StartTime => m_StartTime;
        internal float EndTime => m_EndTime;
        
        public bool IsRecording { get; set; }

        public virtual IEnumerator GenerateAsset(Action callback = null)
        {
            LoadSceneSetup();

            yield return GenerateHeroFrame(callback);
        }

        public virtual IEnumerator GenerateHeroFrame(Action callback = null)
        {
            LoadSceneSetup();

            IsRecording = true;
            int targetFrame = HeroFrame + Time.frameCount;

            RecorderUtils recorder = new RecorderUtils(AssetName, FrameRate);

            while (Time.frameCount <= targetFrame - m_FramesEitherSideOfHero)
            {
                yield return null;
            }

            recorder.RecordImages(Resolution, m_FramesEitherSideOfHero + 1);
            yield return recorder;

            while (Time.frameCount < targetFrame + (2 * m_FramesEitherSideOfHero))
            {
                yield return null;
            }

            recorder.StopRecording();

            // if this is a subclass of AssetDescriptor then we need to wait for the subclass to finish recording, but if it is an AssetDescriptor then we can stop now.
            if (GetType() != typeof(AssetDescriptor))
            {
                while (IsRecording)
                {
                    yield return null;
                }
            }
            IsRecording = false;

            callback?.Invoke();
        }

        [Button()]
        void LoadSceneSetup()
        {
            Camera.main.transform.position = m_CameraPosition;
            Camera.main.transform.eulerAngles = m_CameraRotation;

            LogoController logoController = FindObjectOfType<LogoController>(true);
            if (logoController != null)
            {
                logoController.gameObject.SetActive(m_LogoEnabled);
                logoController.transform.position = m_LogoPosition;
                logoController.transform.eulerAngles = m_LogoRotation;
            }

            FogController fogController = FindObjectOfType<FogController>(true);
            if (fogController != null)
            {
                fogController.gameObject.SetActive(m_FogEnabled);
            }

            BackgroundController backgroundController = FindObjectOfType<BackgroundController>(true);
            if (backgroundController != null)
            {
                backgroundController.transform.position = m_BackgroundPosition;
                backgroundController.transform.eulerAngles = m_BackgroundRotation;
            }
        }

        [Button()]
        internal void SaveSceneSetup()
        {
            if (UnityEditor.EditorUtility.DisplayDialog($"{AssetName} Save Scene Setup", $"Are you sure you want to save the current scene setup to \"{AssetName}\"?", "Yes", "No"))
            {
                m_CameraPosition = Camera.main.transform.position;
                m_CameraRotation = Camera.main.transform.eulerAngles;

                LogoController logoController = FindObjectOfType<LogoController>(true);
                if (logoController != null)
                {
                    m_LogoEnabled = logoController.gameObject.activeSelf;
                    m_LogoPosition = logoController.transform.position;
                    m_LogoRotation = logoController.transform.eulerAngles;
                }

                FogController fogController = FindObjectOfType<FogController>(true);
                if (fogController != null)
                {
                    m_FogEnabled = fogController.gameObject.activeSelf;
                }

                BackgroundController backgroundController = FindObjectOfType<BackgroundController>(true);
                if (backgroundController != null)
                {
                    m_BackgroundPosition = backgroundController.transform.position;
                    m_BackgroundRotation = backgroundController.transform.eulerAngles;
                }
            }
        }
    }
}