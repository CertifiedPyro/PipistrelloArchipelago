using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
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

    private static CancellationTokenSource _cancellationTokenSource;

    public static void Process(DeathLink deathLink)
    {
        if (!ModSettings.DeathLink.Value)
        {
            Melon<PipArchMod>.Logger.Msg("Ignoring death link: death link is disabled.");
            return;
        }

        if (Global.Director.IsPlayerDead())
        {
            Melon<PipArchMod>.Logger.Msg("Ignoring death link: player is already dead.");
            return;
        }

        Melon<PipArchMod>.Logger.Msg($"Received death link: {deathLink.Source}, {deathLink.Cause}");
        Global.State.QueuedDeath = deathLink;
    }

    public static async Task Start()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        try
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded ||
                    !ModSettings.DeathLink.Value ||
                    Global.State.QueuedDeath == null ||
                    !CanKillPlayer(Global.Director.player.state))
                {
                    continue;
                }

                Melon<PipArchMod>.Logger.Msg("Killing player for death link...");
                Global.Director.player.Kill();
                Global.State.Messages.Enqueue(Global.State.QueuedDeath.Cause);
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling death: {e}");
        }
        finally
        {
            Melon<PipArchMod>.Logger.Msg($"Stopping {nameof(DeathLinkHandler)}...");
            _cancellationTokenSource = null;
        }
    }

    public static void End()
    {
        _cancellationTokenSource?.Cancel();
    }

    private static bool CanKillPlayer(ObjectPlayer.State state)
    {
        // Kill if player is not in cutscene, not in menu, not in dialogue, and is not dead.
        return !InvalidStates.Contains(state)
               && Global.Director.uiDialog == null
               && Global.Director.dialoguePanel?.IsOver() != false
               && !Global.Director.IsPlayerDead();
    }
}