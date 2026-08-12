using Il2CppPipistrello;
using MelonLoader;
using System.Collections.ObjectModel;

namespace PipistrelloArchipelago.Handlers;

public static class LocationHandler
{
    /// <summary>
    /// Handles checked locations.
    /// </summary>
    /// <param name="newCheckedLocations">The list of location ids checked.</param>
    public static void Process(ReadOnlyCollection<long> newCheckedLocations)
    {
        if (!Global.State.SaveFileLoaded)
        {
            return;
        }

        var director = Global.Director;
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
                    HandleArchItem(archMapObject);
                    break;
            }
            
        }

        // Ensure map pin changes are queued for a save.
        director.PrepareCheckpoint(false);
    }

    private static void HandleTaxiPhone(Mapvania.Object mapObject)
    {
        // Mark taxi phone interaction.
        var flag = $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsString}{Constants.FLAG_INTERACT_SUFFIX}";
        if (!Global.Director.GetFlagBool(flag))
        {
            Global.Director.SetFlagBool(flag, true);
            Melon<PipArchMod>.Logger.Msg($"Set flag {flag}");
        }
    }

    private static void HandleMoneyBag(Mapvania.Object mapObject)
    {
        // Mark money bag as despawned.
        var director = Global.Director;
        var despawnFlag = $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsStringNoRoom}{Game.FLAG_OBJECT_DESPAWN_SUFFIX}";
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

    private static void HandleArchItem(Mapvania.Object mapObject)
    {
        // Flag the item as acquired, so it doesn't show up again.
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
        var playerAcquiringState = director.player.state == ObjectPlayer.State.AcquiringItem
            || director.player.state == ObjectPlayer.State.AcquiringMegaBattery;
        if (playerAcquiringState)
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
