using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class UIPatches
{
    private static bool _makingPauseMenu;

    /// <summary>
    /// If loading save, reset internal state.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix(int savefileIndex)
    {
        _makingPauseMenu = false;
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
    /// Patch to mark that pause menu is being made.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Menu), nameof(Menu.MakePauseMenu))]
    private static void Menu_MakePauseMenu_Prefix()
    {
        _makingPauseMenu = true;
    }

    /// <summary>
    /// Patch to mark that pause menu is no longer being made.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Menu), nameof(Menu.MakePauseMenu))]
    private static void Menu_MakePauseMenu_Postfix()
    {
        _makingPauseMenu = false;
    }

    /// <summary>
    /// Patch for always showing the upgrades menu.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Game), nameof(Game.GetFlag))]
    private static void Game_GetFlagBool_Postfix(string flag, ref int __result)
    {
        if (_makingPauseMenu && flag == Game.FLAG_ACCOUNTANT_FOUND)
        {
            __result = 2;
        }
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
