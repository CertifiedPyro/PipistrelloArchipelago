using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
using Il2CppPipistrello;
using MelonLoader;
using PipistrelloArchipelago.Handlers;

namespace PipistrelloArchipelago;

public static class ArchipelagoHelper
{
    /// <summary>
    /// Handles the connection to the Archipelago server.
    /// </summary>
    /// <returns>true if the connection succeeded, false otherwise.</returns>
    public static async Task<bool> ConnectAsync()
    {
        if (Global.State.Session != null)
        {
            Global.State.Session.Locations.CheckedLocationsUpdated -= LocationHandler.Process;
            if (Global.State.Session.Socket.Connected)
            {
                await Global.State.Session.Socket.DisconnectAsync();
            }
        }

        var host = ModSettings.Host.Value;
        var port = ModSettings.Port.Value;
        var session = ArchipelagoSessionFactory.CreateSession(host, port);
        session.Locations.CheckedLocationsUpdated += LocationHandler.Process;

        LoginResult result;
        try
        {
            await session.ConnectAsync();
            result = await session.LoginAsync(
                "Pipistrello and the Cursed Yoyo",
                ModSettings.SlotName.Value,
                ItemsHandlingFlags.AllItems,
                password: ModSettings.Password.Value);
        }
        catch (Exception e)
        {
            result = new LoginFailure(e.GetBaseException().Message);
        }

        if (result is LoginSuccessful loginSuccess)
        {
            Global.State = new State
            {
                Session = session,
                SlotData = loginSuccess.SlotData,
                ScoutedLocations = await session.Locations.ScoutLocationsAsync(
                    [.. session.Locations.AllLocations])
            };
            if (!ItemHandler.IsStarted())
            {
                _ = ItemHandler.Start();
            }

            return true;
        }

        Melon<PipArchMod>.Logger.Error($"Failed to connect: {host}:{port}");
        var loginFailure = (LoginFailure)result;
        foreach (var error in loginFailure.Errors)
        {
            Melon<PipArchMod>.Logger.Error(error);
        }

        return false;
    }

    /// <summary>
    /// Handle Archipelago items and locations after connection.
    /// This method must be called after a save file has been selected already.
    /// </summary>
    public static void HandleInitial()
    {
        try
        {
            // Set SaveFileLoaded at the start, so HandleCheckedLocations() will run as expected.
            Global.State.SaveFileLoaded = true;
            var director = Global.Director;

            // Handle remote-checked locations.
            // Archipelago sends every checked location on connection.
            Melon<PipArchMod>.Logger.Msg("Handling remote checked locations...");
            var locationsHelper = Global.State.Session.Locations;
            LocationHandler.Process(locationsHelper.AllLocationsChecked);

            // Handle missed local-checked locations.
            Melon<PipArchMod>.Logger.Msg("Handling local checked locations...");
            var checkedLocationsSet = new HashSet<long>(locationsHelper.AllLocationsChecked);
            var missedLocalLocations = new List<long>();
            foreach (var locationId in locationsHelper.AllMissingLocations)
            {
                // Check physical Archipelago items.
                var objectId = Utils.LocationIdToObjectId(locationId);
                var archObjectId = Utils.IdToArchItemId(objectId);
                var bpFlag = Game.FlagBpContainerAcquired(archObjectId);
                if (director.GetFlagBool(bpFlag))
                {
                    missedLocalLocations.Add(locationId);
                }

                // Check money bags
                var mapObject = Utils.GetMapvaniaObject(objectId);
                if (mapObject == null)
                {
                    continue;
                }

                var moneyBagDespawnFlag =
                    $"{Game.GLOBAL_FLAG_PREFIX}{mapObject.globalObjectId.AsStringNoRoom}{Game.FLAG_OBJECT_DESPAWN_SUFFIX}";
                if (director.GetFlag(moneyBagDespawnFlag) != 0)
                {
                    missedLocalLocations.Add(locationId);
                }
            }

            // Check missed taxi phones.
            foreach (var objectId in director.playerRecord.taxiPhonesUnlocked)
            {
                if (!Utils.IsObjectIdActiveLocation(objectId.AsString))
                {
                    continue;
                }

                var locationId = Utils.ObjectIdToLocationId(objectId.AsString);
                if (!checkedLocationsSet.Contains(locationId))
                {
                    missedLocalLocations.Add(locationId);
                }
            }

            if (missedLocalLocations.Count > 0)
            {
                Melon<PipArchMod>.Logger.Msg(
                    $"Found unsent location check ids: {string.Join(',', missedLocalLocations)}");
                try
                {
                    locationsHelper.CompleteLocationChecks([.. missedLocalLocations]);
                }
                catch (ArchipelagoSocketClosedException ex)
                {
                    Melon<PipArchMod>.Logger.Error($"Could not send location checks: {ex}");
                }
            }

            // Prepare checkpoint to ensure any missing items or locations will be saved.
            director.PrepareCheckpoint(false);
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error("Exception handling initial received items: " + ex);
        }
    }
}