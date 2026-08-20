using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Exceptions;
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
            if (Global.State.DeathLinkService != null)
            {
                Global.State.DeathLinkService.OnDeathLinkReceived -= DeathLinkHandler.HandleDeathLink;
            }

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

            // Handle death link.
            // var deathLinkEnabled = loginSuccess.SlotData.TryGetValue("death_link", out var deathLinkValue)
            //                        && bool.TryParse(deathLinkValue?.ToString(), out var parsedDeathLinkValue)
            //                        && parsedDeathLinkValue;
            var deathLinkEnabled = true;
            if (loginSuccess.SlotData.TryGetValue("death_link_amnesty", out var deathLinkAmnestyValue)
                && int.TryParse(deathLinkAmnestyValue.ToString(), out var parsedDeathLinkAmnestyValue))
            {
                Global.State.DeathLinkAmnesty = parsedDeathLinkAmnestyValue;
            }

            if (deathLinkEnabled)
            {
                Global.State.DeathLinkService = session.CreateDeathLinkService();
                Global.State.DeathLinkService.OnDeathLinkReceived += DeathLinkHandler.HandleDeathLink;
                if (!DeathLinkHandler.IsStarted())
                {
                    _ = DeathLinkHandler.Start();
                }

                Global.State.DeathLinkService.EnableDeathLink();
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