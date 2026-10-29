using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GradualRepair
{
    // Resolve once, validate before installing patches, and use these exact handles at runtime.
    internal static class NativeReferences
    {
        static readonly Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
        internal static int Count => fields.Count;
        static string Key(Type type, string name) => type.FullName + "." + name;
        internal static FieldInfo Field(Type type, string name) => fields.TryGetValue(Key(type, name), out var field) ? field : throw new MissingFieldException(type.FullName, name);
        static void Require(Type owner, string name, Type value)
        {
            var field = AccessTools.Field(owner, name);
            if (field == null || field.FieldType != value || field.IsStatic) throw new MissingFieldException("Required instance field: " + Key(owner, name) + " : " + value.FullName);
            fields.Add(Key(owner, name), field);
        }
        internal static void Initialize()
        {
            fields.Clear();
            Require(typeof(Save), "loadedFileSubdirectory", typeof(string));
            Require(typeof(DoWork), "progressorPrefab", typeof(GameObject));
            Require(typeof(DoWork), "workBarPosition", typeof(Transform));
            Require(typeof(FloorDirt), "workNeeded", typeof(float));
            Require(typeof(PlayerInputs), "_currentMapSelected", typeof(string));
            Require(typeof(GamepadSprite), "actionType", typeof(ActionType));
            Require(typeof(GamepadSprite), "image", typeof(Image));
            Require(typeof(Item), "id", typeof(int));
            Require(typeof(PostboxUI), "elementPrefab", typeof(GameObject));
            Require(typeof(PauseMenuUI), "yesNoDialogue", typeof(YesNoDialogueUI));
            Require(typeof(RecipeElementUI), "inputSlots", typeof(SlotUIRecipe[]));
            Require(typeof(FarmConstructionActionInfoUI), "contentRectTransform", typeof(RectTransform));
            Require(typeof(FarmConstructionActionInfoUI), "normalColor", typeof(Color));
            Require(typeof(FarmConstructionActionInfoUI), "invalidColor", typeof(Color));
            foreach (var name in new[] { "goldText", "silverText", "copperText" }) Require(typeof(MoneyUI), name, typeof(TextMeshProUGUI));
            foreach (var name in new[] { "playerPlanksTextMesh", "playerNailsTextMesh", "playerStonesTextMesh", "playerMortarTextMesh" }) Require(typeof(ConstructionPlayerInfo), name, typeof(TextMeshProUGUI));
            foreach (var name in new[] { "planks", "nails", "stones", "mortar" }) Require(typeof(ConstructionPlayerInfo), name, typeof(Item));
        }
        internal static MethodInfo PatchTarget(Type owner, string name, Type[] args)
        {
            var method = AccessTools.Method(owner, name, args);
            Type result = owner == typeof(Save) && name == "SaveGame" ? typeof(string)
                : owner == typeof(EditorActionsDBAccessor) ? typeof(DecorationTile)
                : name == "HasAnyModificationDone" || name == "ApplyChanges" || name == "InteractInput" || owner == typeof(FloorDirt) && name == "Clean" ? typeof(bool) : typeof(void);
            if (method == null || method.IsStatic != (owner == typeof(EditorActionsDBAccessor) || owner == typeof(ButtonsContext)) || method.ReturnType != result || method.ContainsGenericParameters || !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(args))
                throw new MissingMethodException("Required patch target signature: " + owner.FullName + "." + name);
            return method;
        }
    }
}
