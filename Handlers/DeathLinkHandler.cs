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

    public static void HandleDeathLink(DeathLink deathLink)
    {
        if (Math.Abs(deathLink.Timestamp.ToUnixTimeStamp() - Global.State.LastSentDeathLinkTimestamp) <= 1)
        {
            return;
        }

        Global.State.CurrentDeaths += 1;
    }

    public static async Task Start()
    {
        try
        {
            while (true)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded)
                {
                    continue;
                }

                if (Global.State.CurrentDeaths < Global.State.DeathLinkAmnesty ||
                    !CanKillPlayer(Global.Director.player.state))
                {
                    continue;
                }

                Global.State.CurrentDeaths = 0;
                _handlingDeathLinkDeath = true;
                Global.Director.player.Kill();
                _handlingDeathLinkDeath = false;
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {e}");
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