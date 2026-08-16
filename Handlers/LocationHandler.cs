using Il2CppPipistrello;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

internal static class LocationHandler
{
    /// <summary>
    /// Handles checked locations.
    /// </summary>
    /// <param name="newCheckedLocations">The list of location ids checked.</param>
    public static void Process(IEnumerable<long> newCheckedLocations)
    {
        if (!Global.State.SaveFileLoaded)
        {
            return;
        }

        foreach (var locationId in newCheckedLocations)
        {
            var locationName = Global.State.Session.Locations.GetLocationNameFromId(locationId);
            var objectId = Utils.LocationIdToObjectId(locationId);
            var mapObject = Utils.GetMapvaniaObject(objectId);
            Melon<PipArchMod>.Logger.Msg($"Location checked: {locationName}, {objectId}, {mapObject?.objectDefName}");

            switch (mapObject?.objectDefName)
            {
                case "taxiPhone":
                    HandleTaxiPhone(mapObject);
                    break;
                case "moneyBag":
                    HandleMoneyBag(mapObject);
                    break;
                default:
                    var archObjectId = Utils.IdToArchItemId(objectId);
                    var archMapObject = Utils.GetMapvaniaObject(archObjectId);
                    HandleGenericLocation(archMapObject);
                    break;
            }
        }

        // Ensure map pin changes are queued for a save.
        Global.Director.PrepareCheckpoint(false);
    }

    public static List<long> CheckUnsentLocalLocations()
    {
        Melon<PipArchMod>.Logger.Msg("Checking for unsent local locations...");
        return
        [
            .. CheckUnsentTaxiPhonesLocations(),
            .. CheckUnsentMoneyBagLocations(),
            .. CheckUnsentGenericLocations()
        ];
    }

    private static void HandleTaxiPhone(Mapvania.Object mapObject)
    {
        // Mark taxi phone interaction.
        var flag = $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsString}{Constants.FlagInteractSuffix}";
        if (!Global.Director.GetFlagBool(flag))
        {
            Global.Director.SetFlagBool(flag, true);
            Melon<PipArchMod>.Logger.Msg($"Set flag {flag}");
        }
    }

    private static List<long> CheckUnsentTaxiPhonesLocations()
    {
        var missingLocations = new HashSet<long>(Global.State.Session.Locations.AllMissingLocations);
        var locations = new List<long>();
        foreach (var objectId in Global.Director.playerRecord.taxiPhonesUnlocked)
        {
            if (!Utils.IsObjectIdActiveLocation(objectId.AsString))
            {
                continue;
            }

            var locationId = Utils.ObjectIdToLocationId(objectId.AsString);
            if (missingLocations.Contains(locationId))
            {
                locations.Add(locationId);
            }
        }

        return locations;
    }


    private static void HandleMoneyBag(Mapvania.Object mapObject)
    {
        // Mark money bag as despawned.
        var director = Global.Director;
        var despawnFlag =
            $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsStringNoRoom}{Game.FLAG_OBJECT_DESPAWN_SUFFIX}";
        if (director.GetFlag(despawnFlag) != Game.FLAGVALUE_OBJECT_DESPAWN_PERMANENT)
        {
            director.SetFlag(despawnFlag, Game.FLAGVALUE_OBJECT_DESPAWN_PERMANENT);
            Melon<PipArchMod>.Logger.Msg($"Set flag {despawnFlag}");
        }

        RemoveMapPin(mapObject.globalObjectId.AsString);

        // Destroy object if it is instantiated.
        var obj = Utils.GetObject<ObjectMoneyBag>(mapObject);
        if (obj != null)
        {
            director.DestroyObject(obj);
        }
    }

    private static List<long> CheckUnsentMoneyBagLocations()
    {
        var locations = new List<long>();
        foreach (var locationId in Global.State.Session.Locations.AllMissingLocations)
        {
            var objectId = Utils.LocationIdToObjectId(locationId);
            var mapObject = Utils.GetMapvaniaObject(objectId);
            if (mapObject == null)
            {
                continue;
            }

            var moneyBagDespawnFlag =
                $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsStringNoRoom}{Game.FLAG_OBJECT_DESPAWN_SUFFIX}";
            if (Global.Director.GetFlag(moneyBagDespawnFlag) != 0)
            {
                locations.Add(locationId);
            }
        }

        return locations;
    }

    private static void HandleGenericLocation(Mapvania.Object mapObject)
    {
        // Flag the physical Archipelago item as acquired, so it doesn't show up again.
        var director = Global.Director;
        var flag = Game.FlagBpContainerAcquired(mapObject.globalObjectId.AsString);
        if (!director.GetFlagBool(flag))
        {
            director.SetFlagBool(flag, true);
            Melon<PipArchMod>.Logger.Msg($"Set flag {flag}");
        }

        RemoveMapPin(mapObject.globalObjectId.AsString);

        // Don't destroy the object if it's being held by the player.
        // TODO: Figure out more robust condition to determine if arch item is being acquired right now.
        if (director.player.state is ObjectPlayer.State.AcquiringItem or ObjectPlayer.State.AcquiringMegaBattery)
        {
            return;
        }

        // Destroy object if it is instantiated.
        var obj = Utils.GetObject<ObjectBpContainer>(mapObject);
        if (obj != null)
        {
            director.DestroyObject(obj);
        }
    }

    private static List<long> CheckUnsentGenericLocations()
    {
        var locations = new List<long>();
        foreach (var locationId in Global.State.Session.Locations.AllMissingLocations)
        {
            // Check physical Archipelago items.
            var objectId = Utils.LocationIdToObjectId(locationId);
            var archObjectId = Utils.IdToArchItemId(objectId);
            var bpFlag = Game.FlagBpContainerAcquired(archObjectId);
            if (Global.Director.GetFlagBool(bpFlag))
            {
                locations.Add(locationId);
            }
        }

        return locations;
    }

    private static void RemoveMapPin(string globalObjectId)
    {
        // Remove from playerRecord.mapPins so that Minimap.RefreshPins() sees the pin removed.
        // Remove from playerPendingCheckpoint.mapPins so that the removed map pin is eventually saved.
        var records = new[] { Global.Director.playerRecord, Global.Director.playerPendingCheckpoint };
        foreach (var record in records)
        {
            var mapPins = record.mapPins;
            var moneyBagMapPin = mapPins.ToArray().FirstOrDefault(p => p.objectId.AsString == globalObjectId);

            // Check if the map pin is null, or the game will crash.
            if (moneyBagMapPin != null)
            {
                mapPins.Remove(moneyBagMapPin);
            }
        }
    }
}