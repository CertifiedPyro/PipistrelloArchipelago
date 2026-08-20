using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using MelonLoader;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago;

internal static class Constants
{
    public const string ArchItemObjectIdSuffix = "_architem";
    public const string ArchMediumSpriteName = "arch_medium";
    public const string ArchSmallSpriteName = "arch_small";
    public const string MoneyBagMediumSpriteName = "moneyBag_medium";
    public const string MoneyBagSmallSpriteName = "moneyBag_small";
    public const string FlagInteractSuffix = ":interacted";

    public static readonly string FlagArchipelago = $"{Game.GLOBAL_FLAG_PREFIX}arch";
    public static readonly string FlagLastItemIndex = $"{FlagArchipelago}:lastItemIndex";
}

internal static class ModSettings
{
    public static MelonPreferences_Category Category;
    public static MelonPreferences_Entry<string> Host;
    public static MelonPreferences_Entry<int> Port;
    public static MelonPreferences_Entry<string> SlotName;
    public static MelonPreferences_Entry<string> Password;
}

internal static class Global
{
    public static Director Director = null;
    public static Dictionary<string, string> GlobalObjectIdToLocationName = null;
    public static Dictionary<string, string> LocationNameToGlobalObjectId = null;
    public static State State = new();
}

internal static class Utils
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
        if (Global.State.IsObjectIdActiveLocationCache.TryGetValue(globalObjectId, out var existingValue) &&
            !existingValue)
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
        where T : Object
    {
        if (mapObject == null)
        {
            return null;
        }

        var result = Global.Director.objects.ToArray()
            .FirstOrDefault(o => o.globalObjectId.AsString == mapObject.globalObjectId.AsString);
        return result?.Cast<T>();
    }

    public static void SendLocationCheck(string globalObjectId)
    {
        // Send location check to Archipelago.
        var locationId = ObjectIdToLocationId(globalObjectId);
        Melon<PipArchMod>.Logger.Msg($"Sending location check: {globalObjectId}");

        // Duplicate locations should never happen, but log just in case.
        // We still want the rest of the function to run so the dialogue can get replaced.
        if (!Global.State.Session.Locations.AllMissingLocations.Contains(locationId))
        {
            Melon<PipArchMod>.Logger.Warning(
                $"Duplicate location found: {Global.State.Session.Locations.GetLocationNameFromId(locationId)}");
        }

        try
        {
            Global.State.LocalCheckedLocations.TryAdd(locationId, 1);
            Global.State.Session.Locations.CompleteLocationChecks(locationId);
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

internal class State
{
    public readonly Dictionary<string, bool> IsObjectIdActiveLocationCache = [];
    public readonly ConcurrentDictionary<long, byte> LocalCheckedLocations = [];
    public readonly ConcurrentQueue<string> Messages = new();

    public bool SaveFileLoaded = false;
    public bool ReplaceMoneyBagSprite = false;

    public string DialogueText;
    public bool ShowRemainingDialogue = true;

    public DeathLinkService DeathLinkService;
    public bool QueuedDeath = false;
    public int DeathLinkAmnesty = 1;
    public int CurrentDeaths = 0;

    public ArchipelagoSession Session { get; init; }
    public Dictionary<string, object> SlotData { get; init; }
    public Dictionary<long, ScoutedItemInfo> ScoutedLocations { get; init; }
}