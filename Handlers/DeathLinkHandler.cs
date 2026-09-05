using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

/// <summary>
/// Handler for receiving death links from Archipelago.
/// </summary>
internal static class DeathLinkHandler
{
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
}
