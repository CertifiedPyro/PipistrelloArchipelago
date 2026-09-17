using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

/// <summary>
/// Handler for receiving death links from Archipelago.
/// </summary>
[HarmonyPatch]
internal static class DeathLinkHandler
{
    private static readonly HashSet<ObjectPlayer.State> InvalidStates =
    [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene,
    ];

    /// <summary>
    /// Processes a received death link.
    /// </summary>
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

        if (Global.State.QueuedDeath != null)
        {
            Melon<PipArchMod>.Logger.Msg("Ignoring death link: death link is already queued.");
            return;
        }

        Melon<PipArchMod>.Logger.Msg($"Received death link: {deathLink.Source}, {deathLink.Cause}");
        Global.State.QueuedDeath = deathLink;
    }

    /// <summary>
    /// Patch for handling queued death link.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.Process))]
    private static void ObjectPlayer_Process_Prefix()
    {
        try
        {
            if (!Global.State.SaveFileLoaded
                || !ModSettings.DeathLink.Value
                || Global.State.QueuedDeath == null
                || !CanKillPlayer())
            {
                return;
            }

            // Don't set Global.State.QueuedDeath to null so we know the death comes from death link.
            Melon<PipArchMod>.Logger.Msg("Killing player for death link...");
            Global.Director.player.Kill();
            Global.State.Messages.Enqueue(Global.State.QueuedDeath.Cause);
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling death: {e}");
        }
    }

    private static bool CanKillPlayer() =>
        !InvalidStates.Contains(Global.Director.player.state)
        && Global.Director.uiDialog == null
        && Global.Director.dialoguePanel?.IsOver() != false
        && !Global.Director.IsPlayerDead()
        && !Global.Director.transitionActive
        && Global.Director.player.onGround; // Prevents softlock when exiting safe house
}
