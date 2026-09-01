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

    /// <summary>
    /// Stores the item info at the Mega-Battery location.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Postfix(Object __result)
    {
        if (__result?.TryCast<ObjectMegaBatteryHolder>() == null)
        {
            return;
        }

        var locationId = Utils.ObjectIdToLocationId(__result.globalObjectId.AsString);
        var item = Global.State.ScoutedLocations[locationId];
        _itemName = item.ItemDisplayName;

        var playerName = item.Player.Name.Replace(" ", "[nbsp]");
        _recipientText = Utils.IsLocalItem(item)
            ? "for yourself!"
            : $"for {playerName}!";
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
}
