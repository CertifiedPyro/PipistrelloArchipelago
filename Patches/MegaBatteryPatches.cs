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
    private static readonly HashSet<string> TriggerAreasToRemove = ["dungeon2/lor1089/lor1265", "dungeon3/lor2/lor520"];

    private static string _itemName;
    private static string _recipientText;
    private static string _globalObjectId;
    private static bool _sentLocationCheck;

    /// <summary>
    /// Removes trigger areas that remind the player if they're leaving without the Mega-Battery.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static bool Director_InstantiateFromMap_Prefix(Mapvania.Object mapObj, ref Object __result)
    {
        if (!TriggerAreasToRemove.Contains(mapObj.globalObjectId.AsString))
        {
            return true;
        }

        __result = null;
        return false;
    }

    /// <summary>
    /// Handles Mega-Battery instantiation.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Postfix(ref Object __result)
    {
        if (__result?.TryCast<ObjectMegaBatteryHolder>() is not { } megaBatteryHolder)
        {
            return;
        }

        // Mark Mega-Battery location as checked, without giving the actual Mega-Battery item.
        __result.controlsFlag += Constants.FlagMegaBatterySuffix;

        // Ensure the Mega-Battery holder state matches the modified flag.
        if (Global.Director.GetFlagBool(__result.controlsFlag))
        {
            megaBatteryHolder.state = ObjectMegaBatteryHolder.State.Empty;
        }

        // Store the item info at the Archipelago location.
        var locationId = Utils.ObjectIdToLocationId(__result.globalObjectId.AsString);
        var item = Global.State.ScoutedLocations[locationId];
        _itemName = item.ItemDisplayName;

        var playerName = item.Player.Name.Replace(" ", "[nbsp]");
        _recipientText = Utils.IsLocalItem(item)
            ? "for yourself!"
            : $"for {playerName}!";

        _globalObjectId = __result.globalObjectId.AsString;
        _sentLocationCheck = false;
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
        switch (stringId)
        {
            case "ui_get_megaBattery_main":
                __result = _itemName;
                return false;
            case "ui_get_megaBattery_after":
                __result = _recipientText;
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Handles sending the location check.
    /// </summary>
    [HarmonyPostfix,
     HarmonyPatch(typeof(ObjectMegaBatteryHolder), nameof(ObjectMegaBatteryHolder.GetMegaBatteryAnimState))]
    private static void ObjectMegaBatteryHolder_GetMegaBatteryAnimState_Postfix(
        ObjectMegaBatteryHolder.AnimState __result)
    {
        if (!_sentLocationCheck && __result == ObjectMegaBatteryHolder.AnimState.Hold)
        {
            Utils.SendLocationCheck(_globalObjectId);
            _sentLocationCheck = true;
        }
    }
}
