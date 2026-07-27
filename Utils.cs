using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Exceptions;
using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using MelonLoader;

namespace PipistrelloArchipelago;

static class Constants
{
    public static string ArchItemObjectIdSuffix = "_architem";
    public static string ArchMediumSpriteName = "arch_medium";
    public static string ArchSmallSpriteName = "arch_small";

    public static string FLAG_ARCHIPELAGO = $"{Game.GLOBAL_FLAG_PREFIX}arch";
    public static string FLAG_LAST_ITEM_INDEX = $"{FLAG_ARCHIPELAGO}:lastItemIndex";
}

public static class GlobalState
{
    public static Director Director = null;
    public static Dictionary<string, string> GlobalObjectIdToLocationName = null;
    public static Dictionary<string, string> LocationNameToGlobalObjectId = null;
}

public static class SaveState
{
    public static ArchipelagoSession Session = null;
    public static Dictionary<string, object> SlotData = null;
    public static Dictionary<long, ScoutedItemInfo> ScoutedLocations = null;

    public static ScoutedItemInfo AcquiredPhysicalItem = null;
    public static Queue<string> Messages = new();

    public static void Reset()
    {
        Session = null;
        SlotData = null;
        ScoutedLocations = null;

        AcquiredPhysicalItem = null;
        Messages = new();
    }
}

static class ModSettings
{
    public static MelonPreferences_Category Category;
    public static MelonPreferences_Entry<string> Host;
    public static MelonPreferences_Entry<int> Port;
    public static MelonPreferences_Entry<string> SlotName;
    public static MelonPreferences_Entry<string> Password;
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
        return id != null && id.Contains(Constants.ArchItemObjectIdSuffix);
    }

    public static void SendLocationCheck(string globalObjectId)
    {
        var locationId = GlobalObjectIdToLocationId(globalObjectId);
        SaveState.AcquiredPhysicalItem = SaveState.ScoutedLocations[locationId];
        Melon<PipArchMod>.Logger.Msg($"Sending location check: {globalObjectId}");
        try
        {
            SaveState.Session.Locations.CompleteLocationChecks([locationId]);
        }
        catch (ArchipelagoSocketClosedException ex)
        {
            Melon<PipArchMod>.Logger.Error($"Could not send location check: {ex}");
        }
    }

    public static long GlobalObjectIdToLocationId(string globalObjectId)
    {
        var locationName = GlobalState.GlobalObjectIdToLocationName[globalObjectId];
        var game = SaveState.Session.ConnectionInfo.Game;
        return SaveState.Session.Locations.GetLocationIdFromName(game, locationName);
    }

    public static string LocationIdToGlobalObjectId(long locationId)
    {
        var locationName = SaveState.Session.Locations.GetLocationNameFromId(locationId);
        return GlobalState.LocationNameToGlobalObjectId[locationName];
    }
}
