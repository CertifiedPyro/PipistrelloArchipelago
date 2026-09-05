using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to handle money bags as physical Archipelago objects.
/// </summary>
[HarmonyPatch]
internal class MoneyBagPatches
{
    private static bool _replaceMoneyBagSprite;

    /// <summary>
    /// If loading save, reset internal state.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix()
    {
        _replaceMoneyBagSprite = false;
    }

    /// <summary>
    /// Handles money bags as physical Archipelago items.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Process))]
    private static void ObjectMoneyBag_Process_Postfix(ObjectMoneyBag __instance)
    {
        // Check that save file is actually loaded, since Process() will run before load, for some reason.
        if (!Global.State.SaveFileLoaded)
        {
            return;
        }

        // Check that the object should be swapped.
        var globalObjectId = __instance.globalObjectId.AsString;
        if (!Utils.IsObjectIdActiveLocation(globalObjectId))
        {
            return;
        }

        // If money bag is collected, send location check.
        if (__instance.collected)
        {
            Utils.SendLocationCheck(globalObjectId);
            return;
        }

        // If money bag is destroyed through other means (i.e. moving to new area), return early.
        if (__instance.destroyed)
        {
            return;
        }

        // Money bag should not give money.
        __instance.moneyAmount = 0;

        // Add map pin for money bag.
        // It seems better performance-wise to always add the map pin, vs checking against the existing map pins.
        __instance.UpdateMapPin(Constants.MoneyBagSmallSpriteName);
    }

    /// <summary>
    /// Marks the money bag sprite for replacement.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Draw))]
    private static void ObjectMoneyBag_Draw_Prefix(ObjectMoneyBag __instance)
    {
        if (__instance.director.IsPlayerDeathFreeze() || !__instance.IsVisibleInCamera())
        {
            return;
        }

        // Check that the object should be swapped.
        if (!Utils.IsObjectIdActiveLocation(__instance.globalObjectId.AsString))
        {
            return;
        }

        _replaceMoneyBagSprite = true;
    }

    /// <summary>
    /// Replaces the money bag sprite.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Prefix(ref string sprId)
    {
        if (_replaceMoneyBagSprite && sprId == "objs/moneyBag")
        {
            sprId = Constants.MoneyBagMediumSpriteName;
            _replaceMoneyBagSprite = false;
        }
    }
}
