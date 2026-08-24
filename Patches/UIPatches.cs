using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class UIPatches
{
    private static int _prevAccountantFlagValue;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix(int savefileIndex)
    {
        _prevAccountantFlagValue = 0;
    }

    /// <summary>
    /// Patch for showing Archipelago map pins.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Minimap), nameof(Minimap.RefreshPins))]
    private static void Minimap_RefreshPins_Prefix()
    {
        var mapPins = Global.Director.playerRecord.mapPins;
        for (var i = 0; i < mapPins.Count; i++)
        {
            var mapPin = mapPins[i];
            if (mapPin.pinId != "bpContainer")
            {
                continue;
            }

            // Replace map pins for physical Archipelago items with the Archipelago UI pin.
            var locationName = Global.GlobalObjectIdToLocationName.GetValueOrDefault(mapPin.objectId.AsString);
            if (Utils.IsArchItemId(mapPin.objectId.objectId) || locationName != null)
            {
                mapPin.pinId = Constants.ArchSmallSpriteName;
                mapPins.System_Collections_IList_set_Item(i, mapPin);
            }
        }
    }

    /// <summary>
    /// Patch for always showing the upgrades menu.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Menu), nameof(Menu.MakePauseMenu))]
    private static void Menu_MakePauseMenu_Prefix()
    {
        // Pretend that accountant was found.
        // TODO: Patch GetFlagBool instead to be safer.
        _prevAccountantFlagValue = Global.Director.GetFlag(Game.FLAG_ACCOUNTANT_FOUND);
        Global.Director.SetFlag(Game.FLAG_ACCOUNTANT_FOUND, 2);
    }

    /// <summary>
    /// Patch for reverting state after showing the upgrades menu.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Menu), nameof(Menu.MakePauseMenu))]
    private static void Menu_MakePauseMenu_Postfix()
    {
        Global.Director.SetFlag(Game.FLAG_ACCOUNTANT_FOUND, _prevAccountantFlagValue);
    }

    /// <summary>
    /// Patch for locking all upgrades on the upgrade menu.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Game), nameof(Game.IsUpgradeLocked))]
    private static bool Game_IsUpgradeLocked_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    /// <summary>
    /// Patch for locking all badge refinements in the badge menu.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(UIEquipRefinement), nameof(UIEquipRefinement.MakeConfirmationDialog))]
    private static bool UIEquipRefinement_MakeConfirmationDialog_Prefix()
    {
        return false;
    }
}
