using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Playground
{
    /// <summary>
    /// The float Stat Recipe will upgrade one of the player's float stats, such as move speed.
    /// </summary>
    [CreateAssetMenu(fileName = "Float Stat Recipe", menuName = "Playground/Recipe/Float Stat", order = 1)]
    public class FloatStatRecipe : BaseStatRecipe
    {
        [SerializeField, Tooltip("The amount to add to the current multiplier for the stat. For example, if this value is 0.1 and the current multiplier is 1.5 then the new multiplier will be 1.6.")]
        float additionalMultiplier = 0.1f;
        [SerializeField, Tooltip("The amount to add to the pre-multiplication additive value for the stat. For example if the current pre-multiplier additive is 0.5 and this value is 0.1 then the new value will be 0.6.")]
        float additionalPreMultiplyAdd = 0f;
        [SerializeField, Tooltip("The amount to add to the post-multiplication additive value for the stat. For example if the current post-multiplier additive is 0.5 and this value is 0.1 then the new value will be 0.6.")]
        float additionalPostMultiplyAdd = 0f;

        [Button("Apply Float Modifier (works in game only)")]
        internal override void Apply()
        {
            FloatValueModifier moveSpeed = movementUpgradeManager.GetFloatModifier(statName);
            moveSpeed.multiplier += additionalMultiplier;
            moveSpeed.preMultiplyAdd += additionalPreMultiplyAdd;
            moveSpeed.postMultiplyAdd += additionalPostMultiplyAdd;
        }
    }
}