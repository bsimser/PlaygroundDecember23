using NeoFPS.Samples;
using RogueWave;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueWave.UI
{
    public class RecipeCard : MonoBehaviour
    {
        internal enum RecipeCardType
        {
            Offer,
            AcquiredPermanentMini,
            AcquiredTemporaryMini
        }

        [SerializeField, Tooltip("The type of card this is.")]
        internal RecipeCardType cardType;
        [SerializeField, Tooltip("The UI element for displaying the image for this recipe.")]
        Image image;
        [SerializeField, Tooltip("The UI element for displaying the name and description of this recipe.")]
        MultiInputLabel details;
        [SerializeField, Tooltip("The UI element for displaying the current stack size of this recipe.")]
        MultiInputLabel stackText;
        [SerializeField, Tooltip("The button that will be clicked to select this recipe.")]
        internal MultiInputButton selectionButton;

        internal int stackSize = 1;
        HubController hubController;

        IRecipe _recipe;
        internal IRecipe recipe
        {
            get { return _recipe; }
            set
            {
                _recipe = value;

                if (_recipe == null)
                {
                    gameObject.SetActive(false);
                } else
                {
                    gameObject.SetActive(true);
                }
            }
        }

        private void OnEnable()
        {
            hubController = GetComponentInParent<HubController>();
        }

        private void OnGUI()
        {
            if (recipe == null)
            {
                return;
            }

            switch (cardType)
            {
                case RecipeCardType.Offer:
                    SetupOfferCard();
                    break;
                case RecipeCardType.AcquiredPermanentMini:
                    SetupPermenantlyAcquiredCard();
                    break;
                case RecipeCardType.AcquiredTemporaryMini:
                    SetupAcquiredCard();
                    break;
            }
        }

        private void SetupOfferCard()
        {
            image.sprite = _recipe.HeroImage;
            details.description = recipe.Description;
            selectionButton.label = $"Buy for {_recipe.BuyCost}";
            if (RogueLiteManager.persistentData.currentResources >= _recipe.BuyCost)
            {
                selectionButton.interactable = true;
            }
            else
            {
                selectionButton.interactable = false;
                selectionButton.GetComponent<Image>().color = Color.red;
                selectionButton.label = $"Insufficient Funds ({_recipe.BuyCost})";
            }

            SetUpCommonElements();
        }

        private void SetupPermenantlyAcquiredCard()
        {
            selectionButton.gameObject.SetActive(false);
            SetupAcquiredCard();
        }

        private void SetupAcquiredCard()
        {
            image.sprite = _recipe.Icon;
            selectionButton.label = $"Permanent ({recipe.BuyCost})";
            if (RogueLiteManager.persistentData.currentResources < _recipe.BuyCost)
            {
                selectionButton.interactable = false;
                selectionButton.GetComponent<Image>().color = Color.red;
            }

            SetUpCommonElements();
        }

        private void SetUpCommonElements()
        {
            details.label = recipe.DisplayName;

            if (stackText != null)
            {
                if (recipe.IsStackable)
                {
                    stackText.label = $"{stackSize}/{recipe.MaxStack}";
                } else
                {
                    stackText.label = string.Empty;
                }
            }
        }

        public void MakePermanent()
        {
            RogueLiteManager.runData.Remove(recipe);
            HubController.temporaryRecipes.Remove(recipe);

            RogueLiteManager.persistentData.Add(recipe);
            HubController.permanentRecipes.Add(recipe);

            RogueLiteManager.persistentData.currentResources -= recipe.BuyCost;
            hubController.permanentPanel.isDirty = true;
            hubController.temporaryPanel.isDirty = true;

            GameLog.Info($"Made {recipe} permanent.");
        }
    }
}