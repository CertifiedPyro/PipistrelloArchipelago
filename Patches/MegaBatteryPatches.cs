using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches for handling Mega-Batteries.
/// </summary>
[HarmonyPatch]
internal static class MegaBatteryPatches
{
    private static string _itemName;
    private static string _recipientText;
    private static string _globalObjectId;

    /// <summary>
    /// Handles Mega-Battery instantiation.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Postfix(Object __result)
    {
        if (__result?.TryCast<ObjectMegaBatteryHolder>() == null)
        {
            return;
        }

        // Avoid giving the actual Mega-Battery item.
        __result.controlsFlag += Constants.FlagMegaBatterySuffix;

        // Store the item info at the Archipelago location.
        var locationId = Utils.ObjectIdToLocationId(__result.globalObjectId.AsString);
        var item = Global.State.ScoutedLocations[locationId];
        _itemName = item.ItemDisplayName;

        var playerName = item.Player.Name.Replace(" ", "[nbsp]");
        _recipientText = Utils.IsLocalItem(item)
            ? "for yourself!"
            : $"for {playerName}!";

        _globalObjectId = __result.globalObjectId.AsString;
    }

    /// <summary>
    /// Replaces the Mega-Battery sprite.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Prefix(ref string sprId)
    {
        if (sprId.StartsWith("objs/megaBatteryHolder/megaBattery"))
        {
            sprId = Constants.ArchMediumSpriteName;
        }
    }

    /// <summary>
    /// Replaces the text when acquiring the Mega-Battery.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Localization), nameof(Localization.Get))]
    private static bool Localization_Get_Prefix(string stringId, ref string __result)
    {
        if (stringId == "ui_get_megaBattery_main")
        {
            __result = _itemName;
            return false;
        }

        if (stringId == "ui_get_megaBattery_after")
        {
            __result = _recipientText;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Handles sending the location check.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.SetFlagBool))]
    private static void Director_SetFlagBool_Postfix(string flag, bool value)
    {
        if (!flag.EndsWith(Constants.FlagMegaBatterySuffix) || !value)
        {
            return;
        }

        Utils.SendLocationCheck(_globalObjectId);
    }
}
