using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
public static class UIPatches
{
    private static int _prevAccountantFlagValue;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch(int savefileIndex)
    {
        _prevAccountantFlagValue = 0;
    }

    /// <summary>
    /// Patch for showing Archipelago map pins.
    /// </summary>
    [HarmonyPatch(typeof(Minimap), nameof(Minimap.RefreshPins))]
    [HarmonyPrefix]
    public static void RefreshMinimapPinsPatch()
    {
        var mapPins = GlobalState.Director.playerRecord.mapPins;
        for (var i = 0; i < mapPins.Count; i++)
        {
            var mapPin = mapPins[i];

            // Replace map pins for physical Archipelago items with the Archipelago UI pin.
            // Als replace map pins for the original items.
            var locationName = GlobalState.GlobalObjectIdToLocationName.GetValueOrDefault(mapPin.objectId.AsString);
            if (Utils.IsArchItemId(mapPin.objectId.objectId)
                || (locationName != null && !locationName.Contains("Taxi")))
            {
                mapPin.pinId = Constants.ArchSmallSpriteName;
                mapPins.System_Collections_IList_set_Item(i, mapPin);
            }
        }
    }

    /// <summary>
    /// Patch pause menu to always show upgrades menu.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.MakePauseMenu))]
    [HarmonyPrefix]
    public static void MakePauseMenuPrefixPatch()
    {
        // Pretend that accountant was found.
        _prevAccountantFlagValue = GlobalState.Director.GetFlag(Game.FLAG_ACCOUNTANT_FOUND);
        GlobalState.Director.SetFlag(Game.FLAG_ACCOUNTANT_FOUND, 2);
    }

    /// <summary>
    /// Patch pause menu to revert accountant state.
    /// </summary>
    [HarmonyPatch(typeof(Menu), nameof(Menu.MakePauseMenu))]
    [HarmonyPostfix]
    public static void MakePauseMenuPostfixPatch()
    {
        GlobalState.Director.SetFlag(Game.FLAG_ACCOUNTANT_FOUND, _prevAccountantFlagValue);
    }
}
