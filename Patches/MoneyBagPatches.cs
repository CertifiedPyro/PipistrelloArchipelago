using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal class MoneyBagPatches
{
    /// <summary>
    /// Patch for handling money bags as physical Archipelago items.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Process))]
    private static void ObjectMoneyBag_Process_Postfix(ObjectMoneyBag __instance)
    {
        // Check that save file is actually loaded, since Process() will run before save file finishes loading.
        if (!Global.State.SaveFileLoaded)
        {
            return;
        }

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

        // Money bag should not actually give money.
        __instance.moneyAmount = 0;

        // Add map pin for money bag.
        // It seems better performance-wise to always add the map pin, vs checking against the existing map pins.
        __instance.UpdateMapPin(Constants.MoneyBagSmallSpriteName);
    }

    /// <summary>
    /// Patch for marking money bags sprites for replacement.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Draw))]
    private static void ObjectMoneyBag_Draw_Prefix(ObjectMoneyBag __instance)
    {
        if (__instance.director.IsPlayerDeathFreeze() || !__instance.IsVisibleInCamera())
        {
            return;
        }

        if (!Utils.IsObjectIdActiveLocation(__instance.globalObjectId.AsString))
        {
            return;
        }

        // TODO: Make local state variable instead.
        Global.State.ReplaceMoneyBagSprite = true;
    }

    /// <summary>
    /// Patch for replacing the money bag sprite.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Prefix(ref string sprId)
    {
        if (Global.State.ReplaceMoneyBagSprite && sprId == "objs/moneyBag")
        {
            sprId = Constants.MoneyBagMediumSpriteName;
            Global.State.ReplaceMoneyBagSprite = false;
        }
    }
}
