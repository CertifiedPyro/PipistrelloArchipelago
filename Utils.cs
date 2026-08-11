using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using MelonLoader;
using System.Collections.Concurrent;

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
        // Check cache before checking scouted locations.
        if (Global.State.IsObjectIdActiveLocationCache.TryGetValue(globalObjectId, out var existingValue) && !existingValue)
        {
            return false;
        }

        // Check if location is eligible for replacement.
        var objLocationName = Global.GlobalObjectIdToLocationName.GetValueOrDefault(globalObjectId);
        if (objLocationName == null)
        {
            return false;
        }

        // Check if location is actually replaced.
        var game = Global.State.Session.ConnectionInfo.Game;
        var locationId = Global.State.Session.Locations.GetLocationIdFromName(game, objLocationName);
        var active = Global.State.ScoutedLocations.ContainsKey(locationId);

        // Cache lookup for future queries.
        Global.State.IsObjectIdActiveLocationCache[globalObjectId] = active;
        return active;
    }

    public static Mapvania.Object? GetMapvaniaObject(string globalObjectIdString)
    {
        var parts = globalObjectIdString.Split('/');
        var map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == parts[0]);
        var room = map?.rooms.ToArray().FirstOrDefault(r => r.id == parts[1]);
        var obj = room?.objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == parts[2]);
        return obj;
    }

    public static T? GetObject<T>(Mapvania.Object mapObject)
        where T : Il2CppPipistrello.Object
    {
        if (mapObject == null)
        {
            return null;
        }

        var result = Global.Director.objects.ToArray().FirstOrDefault(o => o.globalObjectId.AsString == mapObject.globalObjectId.AsString);
        return result?.Cast<T>();
    }

    public static void SendLocationCheck(string globalObjectId)
    {
        // Send location check to Archipelago.
        var locationId = ObjectIdToLocationId(globalObjectId);
        Melon<PipArchMod>.Logger.Msg($"Sending location check: {globalObjectId}");

        // Duplicate locations should never happen, but this is here just to be safe.
        if (!Global.State.Session.Locations.AllMissingLocations.Contains(locationId))
        {
            Melon<PipArchMod>.Logger.Warning($"Duplicate location found: {Global.State.Session.Locations.GetLocationNameFromId(locationId)}");
            return;
        }

        try
        {
            Global.State.LocalCheckedLocations.TryAdd(locationId, 1);
            Global.State.Session.Locations.CompleteLocationChecks([locationId]);
        }
        catch (ArchipelagoSocketClosedException ex)
        {
            Melon<PipArchMod>.Logger.Error($"Could not send location check: {ex}");
            return;
        }

        // Create the text to show the player.
        var item = Global.State.ScoutedLocations[locationId];
        var itemName = item.ItemDisplayName.Replace(" ", "[nbsp]");
        var playerName = item.Player.Name.Replace(" ", "[nbsp]");
        var text = item.Player.Slot == Global.State.Session.ConnectionInfo.Slot
            ? $"[instant|You found your [c:blue|{itemName}]!][w:2]"
            : $"[instant|You sent [c:blue|{itemName}] to [c:red|{playerName}]!][w:2]";

        // Determine if text should replace dialogue or be queued for later.
        var mapObject = GetMapvaniaObject(globalObjectId);
        if (mapObject?.objectDefName == "moneyBag")
        {
            Global.State.Messages.Enqueue(text);
        }
        else
        {
            Global.State.DialogueText = text;
            Global.State.ShowRemainingDialogue = mapObject?.objectDefName == "taxiPhone";
        }
    }
}

class State
{
    public ArchipelagoSession Session = null;
    public Dictionary<string, object> SlotData = null;
    public Dictionary<long, ScoutedItemInfo> ScoutedLocations = null;

    public Dictionary<string, bool> IsObjectIdActiveLocationCache = [];
    public ConcurrentDictionary<long, byte> LocalCheckedLocations = [];

    public bool SaveFileLoaded = false;
    public bool ReplaceMoneyBagSprite = false;

    public string DialogueText = null;
    public bool ShowRemainingDialogue = true;
    public ConcurrentQueue<string> Messages = new();
}

