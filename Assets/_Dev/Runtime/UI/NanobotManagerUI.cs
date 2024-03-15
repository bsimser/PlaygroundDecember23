using NeoFPS;
using RogueWave.UI;
using System;
using System.Collections;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using static RogueWave.NanobotManager;

namespace RogueWave
{
    public class NanobotManagerUI : PlayerCharacterHudBase
    {
        [SerializeField, Tooltip("Image elements for displaying the icon of the recipe currently being offered.")]
        [FormerlySerializedAs("recipeIcons")]
        private Image[] offerIcon = null;
        [SerializeField, Tooltip("Text boxes for displaying the name of the recipe currently being offered.")]
        private Text[] offerText = null;
        [SerializeField, Tooltip("The icon for current build, if any.")]
        private Image buildAndRequestIcon;
        [SerializeField, Tooltip("The progress meter to show the current build progress, if any.")]
        private ProgressBar buildAndRequetProgressBar;
        [SerializeField, Tooltip("Text boxes for displaying the current status of the nanobots.")]
        private Text statusText = null;
        [SerializeField, Tooltip("The progress bar for the next level of nanobot.")]
        private ProgressBar nanobotLevelProgressBar = null;

        private NanobotManager nanobotManager;

        protected override void Start()
        {
            OnStatusChanged(Status.Collecting);
            base.Start();
        }

        protected override void OnDestroy()
        {
            if (nanobotManager != null)
            {
                nanobotManager.onRequestSent -= OnRequestSent;
                nanobotManager.onBuildStarted -= OnBuildStarted;
                nanobotManager.onResourcesChanged -= OnResourcesChanged;
                nanobotManager.onStatusChanged -= OnStatusChanged;
                nanobotManager.onNanobotLevelUp -= OnNanobotLevelUp;
                nanobotManager.onOfferChanged -= OnOfferChanged;
            }
            base.OnDestroy();
        }

        public override void OnPlayerCharacterChanged(ICharacter character)
        {
            if (nanobotManager != null)
            {
                nanobotManager.onRequestSent += OnRequestSent;
                nanobotManager.onBuildStarted += OnBuildStarted;
                nanobotManager.onResourcesChanged += OnResourcesChanged;
                nanobotManager.onStatusChanged -= OnStatusChanged;
                nanobotManager.onNanobotLevelUp -= OnNanobotLevelUp;
                nanobotManager.onOfferChanged -= OnOfferChanged;
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
                nanobotManager.onOfferChanged += OnOfferChanged;
                OnOfferChanged(nanobotManager.currentOfferRecipes);

                nanobotManager.onStatusChanged += OnStatusChanged;
                OnStatusChanged(nanobotManager.status);

                nanobotManager.onNanobotLevelUp += OnNanobotLevelUp;

                gameObject.SetActive(true);


            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnNanobotLevelUp(int level, int resourceForNextLevel)
        {
            nanobotLevelProgressBar.MaxValue = resourceForNextLevel;
            nanobotLevelProgressBar.Value = 0;
        }

        protected virtual void OnBuildStarted(IRecipe recipe)
        {
            StartCoroutine(BuildOrRequestProgressCo(recipe));
        }

        protected virtual void OnRequestSent(IRecipe recipe)
        {
            StartCoroutine(BuildOrRequestProgressCo(recipe));
        }

        private IEnumerator BuildOrRequestProgressCo(IRecipe recipe)
        {
            buildAndRequestIcon.sprite = recipe.Icon;

            buildAndRequetProgressBar.MaxValue = recipe.TimeToBuild;
            buildAndRequetProgressBar.Value = 0;

            float buildTime = recipe.TimeToBuild;
            while (buildTime > 0)
            {
                buildAndRequetProgressBar.Value += Time.deltaTime;
                buildTime -= Time.deltaTime;
                yield return null;
            }

            buildAndRequestIcon.sprite = null;
            buildAndRequetProgressBar.Value = 0;
        }

        protected virtual void OnResourcesChanged(float from, float to, float resourcesUntilNextLevel)
        {   
            if (from <= to)
            {
                if (resourcesUntilNextLevel > 0)
                {
                    nanobotLevelProgressBar.Value = (int)(nanobotLevelProgressBar.MaxValue - resourcesUntilNextLevel);
                }
                else
                {
                    nanobotLevelProgressBar.Value = nanobotLevelProgressBar.MaxValue;
                }
            }
        }

        private void OnOfferChanged(IRecipe[] currentOffers)
        {
            for (int i = 0; i < offerIcon.Length; i++)
            {
                if (currentOffers == null)
                {
                    offerIcon[i].sprite = null;
                } 
                else if (i < currentOffers.Length && currentOffers[i] != null)
                {
                    if (currentOffers[i].Icon != null)
                    {
                        offerIcon[i].sprite = currentOffers[i].Icon;
                    } else
                    {
                        if (currentOffers[i].HeroImage == null)
                        {
                            Debug.LogWarning("No icon or hero image for recipe " + currentOffers[i].DisplayName);
                            offerIcon[i].sprite = null;
                            continue;
                        }
                        else
                        {
                            offerIcon[i].sprite = Sprite.Create(currentOffers[i].HeroImage,
                                new Rect(0.0f, 0.0f, currentOffers[i].HeroImage.width,
                                currentOffers[i].HeroImage.height),
                                new Vector2(0.5f, 0.5f),
                                currentOffers[i].HeroImage.width / 50.0f);
                        }
                    }
                    offerText[i].text = currentOffers[i].DisplayName;
                }
                else
                {
                    offerIcon[i].sprite = null;
                    offerText[i].text = string.Empty;
                }
            }
        }

        private void OnStatusChanged(Status status)
        {
            bool showStatus = true;
            switch (status)
            {
                case Status.Collecting:
                    statusText.text = "Collect Resources";
                    break;
                case Status.OfferingRecipe:
                    statusText.text = "Offering";
                    showStatus = false;
                    break;
                case Status.Requesting:
                    statusText.text = "Requesting";
                    break;
                case Status.RequestRecieved:
                    statusText.text = "Recieved";
                    break;
            }

            statusText.enabled = showStatus;
            for (int i = 0; i < offerText.Length; i++)
            {
                if (string.IsNullOrEmpty(offerText[i].text))
                {
                    offerText[i].gameObject.SetActive(false);
                    offerIcon[i].gameObject.SetActive(false);
                } else
                {
                    offerText[i].gameObject.SetActive(!showStatus);
                    offerIcon[i].gameObject.SetActive(!showStatus);
                }
                
            }
        }
    }
}