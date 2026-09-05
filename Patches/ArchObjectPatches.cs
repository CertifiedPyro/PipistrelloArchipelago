using HarmonyLib;
using Il2CppPipistrello;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal class ArchObjectPatches
{
    /// <summary>
    /// Creates physical Archipelago objects.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        // Skip taxi phones and money bags.
        if (mapObj.objectDefName is "taxiPhone" or "moneyBag" or "megaBatteryHolder")
        {
            return;
        }

        // Check that the object should be swapped.
        if (!Utils.IsObjectIdActiveLocation(mapObj.globalObjectId.AsString))
        {
            return;
        }

        // Swap to a physical Archipelago object.
        mapObj.objectDefId = "lor313";
        mapObj.objectDefName = "bpContainer";

        // Object id must be edited like this, instead of assigned directly for some reason.
        var globalObjectId = mapObj.globalObjectId;
        globalObjectId.objectId = Utils.IdToArchItemId(globalObjectId.objectId);
        mapObj.globalObjectId = globalObjectId;

        // TODO: Swap items with each other.
    }

    /// <summary>
    /// Swaps sprites for physical Archipelago objects.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Postfix(Object __result)
    {
        if (__result != null && Utils.IsArchItemId(__result.globalObjectId?.objectId))
        {
            __result.spriteName = Constants.ArchMediumSpriteName;
        }
    }

    /// <summary>
    /// Handles an acquired physical Archipelago object.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Game), nameof(Game.SetBpContainerAcquired))]
    private static bool Game_SetBpContainerAcquired_Prefix(string id, ref bool __result)
    {
        if (!Utils.IsArchItemId(id))
        {
            return true;
        }

        var objectId = Utils.ArchItemIdToId(id);
        Utils.SendLocationCheck(objectId);

        __result = false;
        return false;
    }

    /// <summary>
    /// Handles replacing physical Archipelago object map pins.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectBpContainer), nameof(ObjectBpContainer.Process))]
    private static void ObjectBpContainer_Process_Postfix(ObjectBpContainer __instance)
    {
        // Check that save file is actually loaded, since Process() will run before load, for some reason.
        if (!Global.State.SaveFileLoaded
            || !Utils.IsArchItemId(__instance.globalObjectId.AsString))
        {
            return;
        }

        // Update map pin.
        // It seems better performance-wise to always add the map pin, vs checking against the existing map pins.
        var mapPin = __instance.specialState != Object.SpecialState.Acquiring ? Constants.ArchSmallSpriteName : null;
        __instance.UpdateMapPin(mapPin);
    }
}
