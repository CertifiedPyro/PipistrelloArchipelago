using Archipelago.MultiClient.Net.Enums;
using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;
using UnityEngine;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal static class CorePatches
{
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.Init))]
    private static void Director_Init_Postfix(Director __instance)
    {
        Global.Director = __instance;
    }

    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.LoadProject))]
    private static void Director_LoadProject_Postfix()
    {
        MakeArchMapChanges();
    }

    /// <summary>
    /// Patch for handling new and loaded saves.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix(int savefileIndex)
    {
        // Reload project to reset Mapvania objects.
        // This crashes the game if called twice before loading, but it shouldn't happen here.
        Global.Director.LoadProject();

        // If record doesn't exist, we're loading from a new save file.
        var savefileRecord = Global.Director.savefileRecords[savefileIndex];
        if (savefileRecord == null)
        {
            // Find the South Plaza new-game scenario.
            var southPlazaNames = Localization.GetEntries("location_plaza1").ToArray().Select(e => e.contents);
            var scenario = Game.GetNewGameScenarios().ToArray().First(s => southPlazaNames.Contains(s.name));
            var record = Game.DeserializeRecord(scenario.serializedRecord);
            record.flags[Constants.FlagArchipelago] = 1; // Mark as an Archipelago save.
            record.flags[Game.FLAG_ABILITY_THROW] = 0; // Remove Offstring Throw (obtained in Abandoned Tunnels).

            /* Disable various yoyo trick tutorials. */
            // Disable Around-the-World tutorial.
            var gFlag = Game.GLOBAL_FLAG_PREFIX;
            record.flags[$"{gFlag}city/yug1405{Game.FLAG_OBJECT_USED_SUFFIX}"] = 1; // Disable trigger area.
            record.flags[$"{gFlag}tutorialSpin"] = 1; // Disable tutorial.
            // Disable Sleeper tutorial.
            record.flags[$"{gFlag}city_underground/lor813{Game.FLAG_OBJECT_USED_SUFFIX}"] = 1; // Disable trigger area.
            record.flags[$"{gFlag}city_underground/lor779:finished"] = 1; // Disable NPC.
            record.flags[$"{gFlag}city_underground/lor779:barrier"] = 1; // Lower barrier to south room.

            Global.Director.InitFromRecord(record);
        }

        Global.State.Session.SetClientState(ArchipelagoClientState.ClientPlaying);
        ArchipelagoHelper.HandleSaveFileLoad();
    }

    /// <summary>
    /// Patch for creating physical Archipelago items.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        // Don't replace taxi phones or money bags.
        if (mapObj.objectDefName is "taxiPhone" or "moneyBag")
        {
            return;
        }

        // Check that the object should actually be swapped.
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
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Postfix(Object __result)
    {
        if (__result != null && Utils.IsArchItemId(__result.globalObjectId?.objectId))
        {
            __result.spriteName = Constants.ArchMediumSpriteName;
        }
    }

    /// <summary>
    /// Patch to handle post-InitRoom().
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitRoom))]
    private static void Director_InitRoom_Postfix()
    {
        // Check if Director is null, since apparently this can run before the main menu appears.
        // Normally, PrepareCheckpoint() runs before ProcessObjects() within Director.InitRoom().
        // However, since we're adding map pins to money bags in ProcessObjects(), we need to save those map pins.
        // This way, if a player returns to the safehouse, the map pins are still saved.
        Global.Director?.PrepareCheckpoint(false);
    }

    /// <summary>
    /// Patch for handling physical Archipelago items.
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
    /// Patch to show house puzzles as completed based on the replaced physical Archipelago items.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectWarpArea), nameof(ObjectWarpArea.CalculateIsHousePuzzleCompleted))]
    private static bool ObjectWarpArea_CalculateIsHousePuzzleCompleted_Prefix(
        ObjectWarpArea __instance,
        ref bool __result)
    {
        if (!Global.Director.currentProject.housePuzzleFlags.TryGetValue(
                __instance.globalObjectId.AsString, out var houseFlags))
        {
            return true;
        }

        foreach (var flag in houseFlags)
        {
            // Flag will generally be in the format "g:bpContainer:city/ren223/yug5534:acquired".
            var flagSplit = flag.Split(':');

            // Badges are in the format "g:equip:moneySpoilsUp:acquired" instead.
            // Convert the badge to a global object id first.
            if (flagSplit[1] == "equip")
            {
                var equipMeta = Global.Director.currentProject.equipMeta.ToArray()
                    .First(e => e.equipId == flagSplit[2]);
                flagSplit[2] = equipMeta.globalObjectId.AsString;
            }

            // Check that flag matches expected format. Some flags are just regular (e.g. "g:mirrorHousePrize").
            // Check that the global object id's item is actually swapped.
            var newFlag = flag;
            if (flagSplit.Length >= 3 && Utils.IsObjectIdActiveLocation(flagSplit[2]))
            {
                // Swap flag with corresponding Archipelago item flag.
                flagSplit[1] = "bpContainer";
                flagSplit[2] = Utils.IdToArchItemId(flagSplit[2]);
                // Some equips use the :blueprint flag instead of the :acquired flag.
                if (flagSplit.Length == 4)
                {
                    flagSplit[3] = "acquired";
                }

                // Rejoin flag parts.
                newFlag = string.Join(':', flagSplit);
            }

            if (!Global.Director.GetFlagBool(newFlag))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }

    /// <summary>
    /// Patch for goal state.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InitRoom))]
    private static void GoalPatch(string mapId, string roomId)
    {
        if (mapId != "city" || roomId != "ren4872")
        {
            return;
        }

        Melon<PipArchMod>.Logger.Msg("Goal: North Plaza reached!");
        Global.State.Messages.Enqueue("[instant|You reached your goal of [c:red|North Plaza]!][w:2]");
        Global.State.Session.SetClientState(ArchipelagoClientState.ClientGoal);
    }

    private static void MakeArchMapChanges()
    {
        var map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == "city")!;
        var room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren223")!;
        var objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier1") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "ren223",
                        objectId = "archBarrier1"
                    },
                    position = new Vector2(0, 22 * 16),
                    width = 16,
                    height = 7 * 16,
                    properties = JsonValue.Parse("{\"activationFlag\": true}"),
                    usesFlags = true
                });
        }

        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren4152")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier2") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "ren4152",
                        objectId = "archBarrier2"
                    },
                    position = new Vector2(0, 2 * 16),
                    width = 16,
                    height = 7 * 16,
                    properties = JsonValue.Parse("{\"activationFlag\": true}"),
                    usesFlags = true
                });
        }

        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "ren4064")!;
        objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "archBarrier3") == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor15",
                    objectDefName = "barrier",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "city",
                        roomId = "ren4064",
                        objectId = "archBarrier3"
                    },
                    position = new Vector2(0, 4 * 16),
                    width = 16,
                    height = 2 * 16,
                    properties = JsonValue.Parse("{\"activationFlag\": true}"),
                    usesFlags = true
                });
        }

        // Remove door to skyscraper mini-dungeon.
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "yug2741")!;
        objects = room.objects;
        var objectToRemove = objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "yug2747");
        if (objectToRemove != null)
        {
            objects.Remove(objectToRemove);
        }

        // Remove slime NPC in front of Faria dungeon.
        room = map.rooms.ToArray().FirstOrDefault(r => r.id == "yug108")!;
        objects = room.objects;
        objectToRemove = objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == "yug3097");
        if (objectToRemove != null)
        {
            objects.Remove(objectToRemove);
        }
    }
}
