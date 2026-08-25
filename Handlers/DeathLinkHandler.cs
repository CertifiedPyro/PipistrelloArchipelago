using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using HarmonyLib;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

[HarmonyPatch]
internal static class DeathLinkHandler
{
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
}
