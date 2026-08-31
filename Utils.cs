using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Colors;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using MelonLoader;
using PipistrelloArchipelago.Patches;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago;

internal static class Constants
{
    public const string ArchItemObjectIdSuffix = "_architem";
    public const string ArchMediumSpriteName = "arch_medium";
    public const string ArchSmallSpriteName = "arch_small";
    public const string MoneyBagMediumSpriteName = "arch_moneyBag_medium";
    public const string MoneyBagSmallSpriteName = "arch_moneyBag_small";
    public const string LeverDisabledSpriteName = "arch_lever_disabled";
    public const string FlagInteractSuffix = ":interacted";
    public const string FlagArchipelagoSeedSuffix = ":seed";

    public static readonly string FlagArchipelago = $"{Game.GLOBAL_FLAG_PREFIX}arch";
    public static readonly string FlagLastItemIndex = $"{FlagArchipelago}:lastItemIndex";
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
        if (Global.State.IsObjectIdActiveLocationCache.TryGetValue(globalObjectId, out var existingValue)
            && !existingValue)
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

    public static bool IsLocalItem(ItemInfo item)
    {
        return item.Player.Slot == Global.State.Session.ConnectionInfo.Slot;
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
        var itemColor = GetTextColor(ColorUtils.GetColor(item).ToString());

        var playerName = item.Player.Name.Replace(" ", "[nbsp]");
        var playerColor = IsLocalItem(item)
            ? GetTextColor(ColorUtils.ActivePlayerColor.ToString())
            : GetTextColor(ColorUtils.NonActivePlayerColor.ToString());

        var text = IsLocalItem(item)
            ? $"You found your [c:{itemColor}|{itemName}]!"
            : $"You sent [c:{itemColor}|{itemName}] to [c:{playerColor}|{playerName}]!";

        // Determine if text should replace dialogue or be queued for later.
        var mapObject = GetMapvaniaObject(globalObjectId);
        if (mapObject?.objectDefName == "moneyBag")
        {
            Global.State.Messages.Enqueue(text);
        }
        else
        {
            Global.State.DialogueText = $"[fast|{text}][w:2]";
            Global.State.ShowRemainingDialogue = mapObject?.objectDefName == "taxiPhone";
        }
    }

    /// <summary>
    /// Converts an Archipelago color to a color the game understands.
    /// This will get converted back to the palette color in <see cref="MessagePatches" />.
    /// </summary>
    public static string GetTextColor(string color)
    {
        return color switch
        {
            nameof(Color.White) => null,
            nameof(Color.Black) => "gray",
            nameof(Color.Red) => "red",
            nameof(Color.Green) => "green",
            nameof(Color.Blue) => "blue",
            nameof(Color.Cyan) => "cyan",
            nameof(Color.Magenta) => "player",
            nameof(Color.Yellow) => "yellow",
            nameof(Color.SlateBlue) => "refine",
            nameof(Color.Salmon) => "lightPink",
            nameof(Color.Plum) => "blueprint",
            _ => null
        };
    }
}

internal class State
{
    public readonly Dictionary<string, bool> IsObjectIdActiveLocationCache = [];
    public readonly ConcurrentDictionary<long, byte> LocalCheckedLocations = [];
    public readonly ConcurrentQueue<string> Messages = new();
    public readonly ConcurrentQueue<string> CountdownMessages = new();

    public bool SaveFileLoaded = false;

    public string DialogueText;
    public bool ShowRemainingDialogue = true;

    public DeathLinkService DeathLinkService;
    public int DeathLinkAmnesty = 1;
    public DeathLink QueuedDeath;

    public ArchipelagoSession Session { get; init; }
    public Dictionary<string, object> SlotData { get; init; }
    public Dictionary<long, ScoutedItemInfo> ScoutedLocations { get; init; }
    public bool RaceMode { get; init; }
}
