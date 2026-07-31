using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
public static class CorePatches
{
    [HarmonyPatch(typeof(Director), nameof(Director.Init))]
    [HarmonyPostfix]
    public static void DirectorInitPatch(Director __instance)
    {
        GlobalState.Director = __instance;
    }

    /// <summary>
    /// Patch for handling new and loaded saves.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch(int savefileIndex)
    {
        // Reload project to reset Mapvania objects.
        // This crashes the game if called twice before loading, but it shouldn't happen here.
        GlobalState.Director.LoadProject();

        // If record exists, we're loading from existing save file.
        var savefileRecord = GlobalState.Director.savefileRecords[savefileIndex];
        if (savefileRecord != null)
        {
            SaveState.Session.SetClientState(Archipelago.MultiClient.Net.Enums.ArchipelagoClientState.ClientPlaying);
            ArchipelagoHelper.InitialHandler();
            return;
        }

        // Find the South Plaza new-game scenario.
        var southPlazaNames = Localization.GetEntries("location_plaza1").ToArray().Select(e => e.contents);
        foreach (var scenario in Game.GetNewGameScenarios())
        {
            if (southPlazaNames.Contains(scenario.name))
            {
                var record = Game.DeserializeRecord(scenario.serializedRecord);
                record.flags[Game.FLAG_ABILITY_THROW] = 0;  // Remove Offstring Throw (obtained in Abandoned Tunnels).
                record.flags[Constants.FLAG_ARCHIPELAGO] = 1;  // Mark as an Archipelago save.
                GlobalState.Director.InitFromRecord(record);

                SaveState.Session.SetClientState(Archipelago.MultiClient.Net.Enums.ArchipelagoClientState.ClientPlaying);
                ArchipelagoHelper.InitialHandler();
            }
        }
    }

    /// <summary>
    /// Patch for creating physical Archipelago items.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    [HarmonyPrefix]
    public static void InstantiateFromMapPrefixPatch(ref Mapvania.Object mapObj)
    {
        // Don't replace taxi phones or money bags.
        if (mapObj.objectDefName == "taxiPhone" || mapObj.objectDefName == "moneyBag")
        {
            return;
        }

        if (!Utils.IsObjectIdActiveLocation(mapObj.globalObjectId.AsString))
        {
            return;
        }

        // Swap to a physical Archipelago item.
        mapObj.objectDefId = "lor313";
        mapObj.objectDefName = "bpContainer";

        // Object id must be edited like this, instead of assigned directly for some reason.
        var globalObjectId = mapObj.globalObjectId;
        globalObjectId.objectId = Utils.IdToArchItemId(globalObjectId.objectId);
        mapObj.globalObjectId = globalObjectId;

        // TODO: Swap items with each other.
    }

    /// <summary>
    /// Patch for handling created Archipelago items.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    [HarmonyPostfix]
    public static void InstantiateFromMapPostfixPatch(Il2CppPipistrello.Object __result)
    {
        if (Utils.IsArchItemId(__result?.globalObjectId?.objectId))
        {
            // Replace sprite.
            __result.spriteName = Constants.ArchMediumSpriteName;
        }
    }

    /// <summary>
    /// Patch for handling physical Archipelago items (disguised as BP containers).
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.SetBpContainerAcquired))]
    [HarmonyPrefix]
    public static bool HandlePhysicalArchItemPatch(string id, ref bool __result)
    {
        // If this is an physical Archipelago item pretending to be a BP container,
        // don't actually pick up the BP container.
        if (Utils.IsArchItemId(id))
        {
            var objectId = Utils.ArchItemIdToId(id);
            Utils.SendLocationCheck(objectId);

            __result = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Patch for handling money bags as physical Archipelago items.
    /// </summary>
    [HarmonyPatch(typeof(ObjectMoneyBag), nameof(ObjectMoneyBag.Process))]
    [HarmonyPostfix]
    public static void MoneyBagProcessPatch(ObjectMoneyBag __instance)
    {
        // Check that save file is actually loaded, since Process() will run before save file finishes loading.
        if (!SaveState.SaveFileLoaded)
        {
            return;
        }

        var globalObjectId = __instance.globalObjectId.AsString;
        if (!Utils.IsObjectIdActiveLocation(globalObjectId))
        {
            return;
        }

        // Add map pin for money bag if it's missing.
        // This needs to run continuously, since loading a new room/area removes these map pins.
        // Note: These map pins also aren't saved the record for some raosn.
        var mapPins = GlobalState.Director.playerRecord.mapPins;
        var existingPin = mapPins.ToArray().Any(p => p.objectId.AsString == globalObjectId);
        if (!existingPin)
        {
            // TODO: Fix map pins not going away on pickup
            __instance.UpdateMapPin(Constants.MoneyBagSmallSpriteName);

            // Money bag should not actually give money.
            __instance.moneyAmount = 0;
        }
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

        SaveState.ReplaceMoneyBagSprite = true;
    }

    /// <summary>
    /// Patch for replacing the money bag sprite.
    /// </summary>
    [HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    [HarmonyPrefix]
    public static void GetSpritePatch(ref string sprId)
    {
        if (SaveState.ReplaceMoneyBagSprite && sprId == "objs/moneyBag")
        {
            sprId = Constants.MoneyBagMediumSpriteName;
            SaveState.ReplaceMoneyBagSprite = false;
        }
    }

    //[HarmonyPatch(typeof(ObjectWarpArea))]
    //public class ObjectWarpAreaPatch
    //{
    //    [HarmonyPatch(nameof(ObjectWarpArea.CalculateIsHousePuzzleCompleted))]
    //    public static bool Prefix(ObjectWarpArea __instance, ref bool __result)
    //    {
    //        if (!GlobalState.Director.currentProject.housePuzzleFlags.TryGetValue(__instance.globalObjectId.AsString, out var houseFlags))
    //        {
    //            return true;
    //        }

    //        foreach (var flag in houseFlags)
    //        {
    //            // TODO: Handle other types of objects
    //            var newFlag = flag;
    //            if (flag.StartsWith(Game.FLAG_EQUIP_PREFIX))
    //            {
    //                var startIndex = Game.FLAG_EQUIP_PREFIX.Length;
    //                var endIndex = flag.IndexOf(':', startIndex);
    //                var equipId = flag[startIndex..endIndex];

    //                // Find equip based on name
    //                Game.GlobalObjectId equipGlobalObjectId = null;
    //                foreach (var meta in GlobalState.Director.currentProject.equipMeta)
    //                {
    //                    if (meta.equipId == equipId)
    //                    {
    //                        equipGlobalObjectId = meta.globalObjectId;
    //                    }
    //                }

    //                var isSwapped = GlobalState.SwappedItems.TryGetValue(equipGlobalObjectId.AsString, out var swappedGlobalObjectId);
    //                if (isSwapped)
    //                {
    //                    // Swap to an archipelago item flag.
    //                    if (swappedGlobalObjectId.mapId == null && swappedGlobalObjectId.roomId == null && swappedGlobalObjectId.objectId == null)
    //                    {
    //                        newFlag = Game.FlagBpContainerAcquired(equipGlobalObjectId.AsString + Constants.ArchItemObjectIdSuffix);
    //                    }
    //                    // TODO: Swap items with each other
    //                }
    //            }

    //            if (!GlobalState.Director.GetFlagBool(newFlag))
    //            {
    //                __result = false;
    //                return false;
    //            }
    //        }

    //        __result = true;
    //        return false;
    //    }
    //}

    /// <summary>
    /// Patch for goal state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitRoom))]
    public class DirectorInitRoomPatch
    {
        public static void Prefix(string mapId, string roomId)
        {
            if (mapId == "city" && roomId == "ren4872")
            {
                Melon<PipArchMod>.Logger.Msg("Goal: North Plaza reached!");
                var text = $"[instant|You reached your goal of [c:red|North Plaza]!][w:2]";
                SaveState.Messages.Enqueue(text);
                SaveState.Session.SetClientState(
                    Archipelago.MultiClient.Net.Enums.ArchipelagoClientState.ClientGoal);
            }
        }
    }
}
