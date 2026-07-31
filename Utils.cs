using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using Il2CppSystem.IO;
using MelonLoader;

namespace PipistrelloArchipelago;

static class Constants
{
    public static string ArchItemObjectIdSuffix = "_architem";
    public static string ArchMediumSpriteName = "arch_medium";
    public static string ArchSmallSpriteName = "arch_small";
    public static string MoneyBagMediumSpriteName = "moneyBag_medium";
    public static string MoneyBagSmallSpriteName = "moneyBag_small";

    public static string FLAG_ARCHIPELAGO = $"{Game.GLOBAL_FLAG_PREFIX}arch";
    public static string FLAG_LAST_ITEM_INDEX = $"{FLAG_ARCHIPELAGO}:lastItemIndex";
    public static string FLAG_INTERACT_SUFFIX = ":interacted";
}

static class ModSettings
{
    public static MelonPreferences_Category Category;
    public static MelonPreferences_Entry<string> Host;
    public static MelonPreferences_Entry<int> Port;
    public static MelonPreferences_Entry<string> SlotName;
    public static MelonPreferences_Entry<string> Password;
}

static class Global
{
    public static Director Director = null;
    public static Dictionary<string, string> GlobalObjectIdToLocationName = null;
    public static Dictionary<string, string> LocationNameToGlobalObjectId = null;
    public static State State = new();
}

static class Utils
{
    public static string IdToArchItemId(string id)
    {
        return id + Constants.ArchItemObjectIdSuffix;
    }

    public static string ArchItemIdToId(string archItemId)
    {
        return archItemId[..^Constants.ArchItemObjectIdSuffix.Length];
    }

    public static bool IsArchItemId(string id)
    {
        return id != null && id.EndsWith(Constants.ArchItemObjectIdSuffix);
    }

    public static long ObjectIdToLocationId(string globalObjectId)
    {
        var locationName = Global.GlobalObjectIdToLocationName[globalObjectId];
        var game = Global.State.Session.ConnectionInfo.Game;
        return Global.State.Session.Locations.GetLocationIdFromName(game, locationName);
    }

    public static string LocationIdToObjectId(long locationId)
    {
        var locationName = Global.State.Session.Locations.GetLocationNameFromId(locationId);
        return Global.LocationNameToGlobalObjectId[locationName];
    }

    public static bool IsObjectIdActiveLocation(string globalObjectId)
    {
        if (Global.State.IsObjectIdActiveLocationCache.TryGetValue(globalObjectId, out var existingValue) && !existingValue)
        {
            return false;
        }

        // Check if item should actually be swapped.
        var objLocationName = Global.GlobalObjectIdToLocationName.GetValueOrDefault(globalObjectId);
        if (objLocationName == null)
        {
            return false;
        }

        var game = Global.State.Session.ConnectionInfo.Game;
        var locationId = Global.State.Session.Locations.GetLocationIdFromName(game, objLocationName);
        var active = Global.State.ScoutedLocations.ContainsKey(locationId);
        Global.State.IsObjectIdActiveLocationCache[globalObjectId] = active;
        return active;
    }

    public static Mapvania.Object? GetMapvaniaObject(string globalObjectIdString)
    {
        var parts = globalObjectIdString.Split('/');
        var map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == parts[0]);
        var room = map.rooms.ToArray().FirstOrDefault(r => r.id == parts[1]);
        var obj = room.objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == parts[2]);
        return obj;
    }

    public static T? GetObjectOrNew<T>(Mapvania.Object mapObject, bool instantiate = true)
        where T : Il2CppPipistrello.Object
    {
        foreach (var obj in Global.Director.IterateObjectsOfType<T>().ToArray())
        {
            if (obj.globalObjectId.AsString == mapObject.globalObjectId.AsString)
            {
                return obj;
            }
        }

        return instantiate ? Global.Director.InstantiateRemotely(mapObject.globalObjectId).Cast<T>() : null;
    }

    public static void SendLocationCheck(string globalObjectId)
    {
        var locationId = ObjectIdToLocationId(globalObjectId);
        Global.State.AcquiredPhysicalItem = Global.State.ScoutedLocations[locationId];
        Melon<PipArchMod>.Logger.Msg($"Sending location check: {globalObjectId}");
        try
        {
            Global.State.Session.Locations.CompleteLocationChecks([locationId]);
        }
        catch (ArchipelagoSocketClosedException ex)
        {
            Melon<PipArchMod>.Logger.Error($"Could not send location check: {ex}");
        }
    }
}

class State
{
    public ArchipelagoSession Session = null;
    public Dictionary<string, object> SlotData = null;
    public Dictionary<long, ScoutedItemInfo> ScoutedLocations = null;

    public Dictionary<string, bool> IsObjectIdActiveLocationCache = [];

    public bool SaveFileLoaded = false;
    public bool ReplaceMoneyBagSprite = false;

    public ScoutedItemInfo AcquiredPhysicalItem = null;
    public Queue<string> Messages = new();
}

