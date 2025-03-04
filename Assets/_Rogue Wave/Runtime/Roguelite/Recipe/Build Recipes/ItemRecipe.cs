﻿using NeoFPS;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace RogueWave
{
    /// <summary>
    /// Creates a recipe for an item. This is a generic class that can be used to create any item.
    /// </summary>
    /// <typeparam name="T">The type of item that will be created.</typeparam>
    /// <seealso cref="AmmoRecipe"/>
    /// <seealso cref="WeaponRecipe"/>
    /// <seealso cref="ToolRecipe"/>
    [CreateAssetMenu(fileName = "Item Pickup Recipe", menuName = "Rogue Wave/Recipe/Generic Item Pickup", order = 100)]
    public class ItemRecipe : GenericItemRecipe<Pickup>
    {
        public override string Category => "Item";
    }
}