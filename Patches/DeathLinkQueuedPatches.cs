using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to handle queued death links.
/// </summary>
[HarmonyPatch]
internal static class DeathLinkQueuedPatches
{
    private static readonly HashSet<ObjectPlayer.State> InvalidStates =
    [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene
    ];

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

    private static bool CanKillPlayer()
    {
        // Kill if player is not in cutscene, not in menu, not in dialogue, and is not dead.
        return !InvalidStates.Contains(Global.Director.player.state)
               && Global.Director.uiDialog == null
               && Global.Director.dialoguePanel?.IsOver() != false
               && !Global.Director.IsPlayerDead();
    }
}
