using Il2CppPipistrello;
using MelonLoader;
using System.Collections.ObjectModel;

namespace PipistrelloArchipelago.Handlers;

public class LocationHandler
{
    /// <summary>
    /// Handles checked locations.
    /// </summary>
    /// <param name="newCheckedLocations">The list of location ids checked.</param>
    /// <summary>
    /// Handle checked locations.
    /// </summary>
    /// <param name="newCheckedLocations">The list of location ids checked.</param>
    public static void HandleCheckedLocations(ReadOnlyCollection<long> newCheckedLocations)
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
            // Remove from playerRecord.mapPins so that Minimap.RefreshPins() sees the pin removed.
            // Remove from playerPendingCheckpoint.mapPins so that the removed map pin is eventually saved.
            var mapPinsList = new[] { director.playerRecord.mapPins, director.playerPendingCheckpoint.mapPins };
            Melon<PipArchMod>.Logger.Msg($"Location checked: {locationName}, {objectId}, {mapObject?.objectDefName}");

            if (mapObject?.objectDefName == "taxiPhone")
            {
                // Mark taxi phone interaction.
                var taxiFlag = $"{Game.GLOBAL_FLAG_PREFIX}{objectId}{Constants.FLAG_INTERACT_SUFFIX}";
                director.SetFlagBool(taxiFlag, true);
                Melon<PipArchMod>.Logger.Msg($"Set flag {taxiFlag}");
                continue;
            }

            if (mapObject?.objectDefName == "moneyBag")
            {
                // Mark money bag as despawned.
                var moneyBagDespawnFlag = $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsStringNoRoom}{Game.FLAG_OBJECT_DESPAWN_SUFFIX}";
                director.SetFlag(moneyBagDespawnFlag, Game.FLAGVALUE_OBJECT_DESPAWN_PERMANENT);
                Melon<PipArchMod>.Logger.Msg($"Set flag {moneyBagDespawnFlag}");

                // Remove the map pin.
                // We must check if the resulting map pin is null, or the game can crash.
                foreach (var mapPins in mapPinsList)
                {
                    var moneyBagMapPin = mapPins.ToArray().FirstOrDefault(p => p.objectId.AsString == objectId);
                    if (moneyBagMapPin != null)
                    {
                        mapPins.Remove(moneyBagMapPin);
                    }
                }

                // Destroy object if it is instantiated.
                var moneyBag = Utils.GetObject<ObjectMoneyBag>(mapObject);
                if (moneyBag != null)
                {
                    director.DestroyObject(moneyBag);
                }

                continue;
            }

            // Flag the item as acquired, so it doesn't show up again.
            var archObjectId = Utils.IdToArchItemId(objectId);
            var flag = Game.FlagBpContainerAcquired(archObjectId);
            if (!director.GetFlagBool(flag))
            {
                director.SetFlagBool(flag, true);
                Melon<PipArchMod>.Logger.Msg($"Set flag {flag}");
            }

            // Remove map pin.
            // We must check if the resulting map pin is null, or the game can crash.
            foreach (var mapPins in mapPinsList)
            {
                var archMapPin = mapPins.ToArray().FirstOrDefault(p => p.objectId.AsString == archObjectId);
                if (archMapPin != null)
                {
                    mapPins.Remove(archMapPin);
                }
            }

            // Destroy object if it is instantiated and not being held by the player.
            mapObject = Utils.GetMapvaniaObject(archObjectId);
            var archItem = Utils.GetObject<ObjectBpContainer>(mapObject);
            var playerAcquiringState = director.player.state == ObjectPlayer.State.AcquiringItem
                || director.player.state == ObjectPlayer.State.AcquiringMegaBattery;
            // TODO: Figure out more robust condition to determine if arch item is being acquired right now.
            if (archItem != null && !playerAcquiringState)
            {
                director.DestroyObject(archItem);
            }
        }

        // Ensure map pin changes are queued for a save.
        director.PrepareCheckpoint(false);
    }
}
