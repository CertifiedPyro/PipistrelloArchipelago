using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
using MelonLoader;
using Newtonsoft.Json.Linq;
using PipistrelloArchipelago.Handlers;

namespace PipistrelloArchipelago;

public static class ArchipelagoHelper
{
    /// <summary>
    /// Handles the connection to the Archipelago server.
    /// </summary>
    /// <returns>true if the connection succeeded, false otherwise.</returns>
    public static async Task<(bool, string)> ConnectAsync()
    {
        await DisconnectAsync();

        var host = ModSettings.Host.Value;
        var port = ModSettings.Port.Value;
        var slotName = ModSettings.SlotName.Value;

        ArchipelagoSession session = null;
        LoginResult result;
        try
        {
            session = ArchipelagoSessionFactory.CreateSession(host, port);
            session.Locations.CheckedLocationsUpdated += LocationHandler.Process;
            session.MessageLog.OnMessageReceived += LogMessageHandler.Process;
            // TODO: add listener for session.Socket.ErrorReceived 

            await session.ConnectAsync();
            result = await session.LoginAsync(
                "Pipistrello and the Cursed Yoyo",
                slotName,
                ItemsHandlingFlags.AllItems,
                password: ModSettings.Password.Value);
        }
        catch (Exception e)
        {
            // ConnectAsync() gives a TaskCancelledException if timing out.
            // We want the failure to match the connection failure in TryConnectAndLogin()
            result = e.GetBaseException() is OperationCanceledException
                ? new LoginFailure("Connection timed out.")
                : new LoginFailure(e.GetBaseException().Message);
        }

        if (result is not LoginSuccessful loginSuccess)
        {
            var loginFailure = (LoginFailure)result;
            var failureStatus = $"Failed to connect: {host}:{port}\n{string.Join("\n", loginFailure.Errors)}";
            Melon<PipArchMod>.Logger.Error(failureStatus);
            return (false, failureStatus);
        }

        var successStatus = $"Connected: {host}:{port}\nSlot: {slotName}";
        Melon<PipArchMod>.Logger.Msg(successStatus);
        Global.State = new State
        {
            Session = session,
            SlotData = loginSuccess.SlotData,
            ScoutedLocations = await session.Locations.ScoutLocationsAsync([.. session.Locations.AllLocations]),
            RaceMode = await session.DataStorage.GetRaceModeAsync()
        };
        _ = ItemHandler.Start();

        // Get the options from the slot data.
        if (loginSuccess.SlotData.TryGetValue("options", out var optionsObj) && optionsObj is JObject options)
        {
            // Handle death link.
            var deathLinkEnabled = options.TryGetValue("death_link", out var deathLinkValue)
                                   && deathLinkValue.ToString() == "1";
            if (loginSuccess.SlotData.TryGetValue("death_link_amnesty", out var deathLinkAmnestyValue)
                && int.TryParse(deathLinkAmnestyValue.ToString(), out var parsedDeathLinkAmnestyValue))
            {
                Global.State.DeathLinkAmnesty = parsedDeathLinkAmnestyValue;
            }

            if (deathLinkEnabled)
            {
                Melon<PipArchMod>.Logger.Msg("Enabling death link.");
                Global.State.DeathLinkService = session.CreateDeathLinkService();
                Global.State.DeathLinkService.OnDeathLinkReceived += DeathLinkHandler.Process;

                Global.State.DeathLinkService.EnableDeathLink();
                ModSettings.DeathLink.Value = true;
            }
        }

        return (true, successStatus);
    }

    public static async Task DisconnectAsync()
    {
        Global.State.SaveFileLoaded = false;
        if (Global.State.Session == null)
        {
            return;
        }

        Global.State.Session.Locations.CheckedLocationsUpdated -= LocationHandler.Process;
        Global.State.Session.MessageLog.OnMessageReceived -= LogMessageHandler.Process;
        Global.State.DeathLinkService?.OnDeathLinkReceived -= DeathLinkHandler.Process;

        ItemHandler.End();

        if (Global.State.Session.Socket.Connected)
        {
            await Global.State.Session.Socket.DisconnectAsync();
        }
    }

    /// <summary>
    /// Handles Archipelago items and locations after a save file is loaded.
    /// This method must be called after a save file has already been loaded.
    /// </summary>
    public static void HandleSaveFileLoad()
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

            // Check for any unsent local location checks.
            var unsentLocations = LocationHandler.CheckUnsentLocalLocations();
            if (unsentLocations.Count > 0)
            {
                Melon<PipArchMod>.Logger.Msg($"Found unsent location check ids: {string.Join(',', unsentLocations)}");
                try
                {
                    locationsHelper.CompleteLocationChecks([.. unsentLocations]);
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
