using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
public class MoneyBagPatches
{
    /// <summary>
    /// Patch for handling money bags as physical Archipelago items.
    /// </summary>
    [HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Process))]
    [HarmonyPostfix]
    public static void MoneyBagProcessPatch(ObjectMoneyBag __instance)
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

        // If money bag is destroyed, remove map pin.
        if (__instance.destroyed)
        {
            __instance.UpdateMapPin(null);
            return;
        }

        // Money bag should not actually give money.
        __instance.moneyAmount = 0;

        // Add map pin for money bag.
        // It seems better performance-wise to always add the map pin, vs checking against the existing map pins.
        // Note: These map pins also aren't saved the record for some raosn.
        __instance.UpdateMapPin(Constants.MoneyBagSmallSpriteName);
    }

    /// <summary>
    /// Patch for mark money bags sprites for replacement.
    /// </summary>
    [HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Draw))]
    [HarmonyPrefix]
    public static void DrawMoneyBagPatch(ObjectMoneyBag __instance)
    {
        if (__instance.director.IsPlayerDeathFreeze() || !__instance.IsVisibleInCamera())
        {
            return;
        }

        if (!Utils.IsObjectIdActiveLocation(__instance.globalObjectId.AsString))
        {
            return;
        }

        Global.State.ReplaceMoneyBagSprite = true;
    }

    /// <summary>
    /// Patch for replacing the money bag sprite.
    /// </summary>
    [HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    [HarmonyPrefix]
    public static void GetSpritePatch(ref string sprId)
    {
        if (Global.State.ReplaceMoneyBagSprite && sprId == "objs/moneyBag")
        {
            sprId = Constants.MoneyBagMediumSpriteName;
            Global.State.ReplaceMoneyBagSprite = false;
        }
    }
}
