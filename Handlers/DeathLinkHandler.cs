using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Converters;
using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

[HarmonyPatch]
internal static class DeathLinkHandler
{
    private static readonly HashSet<ObjectPlayer.State> InvalidStates =
    [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene
    ];

    private static bool _handlingDeathLinkDeath;
    private static bool _isDeathLinkHandlerEnabled;

    public static bool IsStarted()
    {
        return _isDeathLinkHandlerEnabled;
    }

    public static void HandleDeathLink(DeathLink deathLink)
    {
        Melon<PipArchMod>.Logger.Msg($"Received death link: {deathLink.Source}, {deathLink.Cause}");
        Global.State.QueuedDeath = true;
    }

    public static async Task Start()
    {
        _isDeathLinkHandlerEnabled = true;
        try
        {
            while (true)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded ||
                    !Global.State.QueuedDeath ||
                    !CanKillPlayer(Global.Director.player.state) ||
                    _handlingDeathLinkDeath)
                {
                    continue;
                }

                Melon<PipArchMod>.Logger.Msg("Killing player...");
                _handlingDeathLinkDeath = true;
                Global.Director.player.Kill();
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {e}");
        }
        finally
        {
            _handlingDeathLinkDeath = false;
            _isDeathLinkHandlerEnabled = false;
        }
    }

    [HarmonyPatch(typeof(Director), nameof(Director.HandleDeath))]
    [HarmonyPrefix]
    public static void HandleDeathPatch()
    {
        if (Global.State.QueuedDeath)
        {
            Global.State.QueuedDeath = false;
            _handlingDeathLinkDeath = false;
            return;
        }

        Global.State.CurrentDeaths += 1;
        if (Global.State.CurrentDeaths >= Global.State.DeathLinkAmnesty)
        {
            Global.State.CurrentDeaths = 0;

            var playerName = Global.State.Session.Players.GetPlayerAlias(Global.State.Session.ConnectionInfo.Slot);
            var cause = $"{playerName} died.";
            Melon<PipArchMod>.Logger.Msg($"Sending death link: {cause}");
            Global.State.DeathLinkService.SendDeathLink(new DeathLink(playerName, cause));
        }
    }

    private static bool CanKillPlayer(ObjectPlayer.State state)
    {
        // Kill if player is not in cutscene, not in menu, and not in dialogue.
        return !InvalidStates.Contains(state)
               && Global.Director.uiDialog == null
               && Global.Director.dialoguePanel?.IsOver() != false;
    }
}