using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patch for dialogue when a virtual Archipelago item is received.
/// </summary>
[HarmonyPatch]
public static class InstructionPanelPatch
{
    private const long TextShowTimeMs = 3000;
    private const long TextCooldownTimeMs = 250;
    private const long IdleStateMs = 750;

    private static readonly HashSet<ObjectPlayer.State> InvalidStates = [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene,
    ];

    private static long? TextShowStartMs;
    private static long? TextCooldownStartMs;
    private static long? IdleStartMs;
    private static bool validPreviousState;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch()
    {
        TextShowStartMs = null;
        TextCooldownStartMs = null;
        IdleStartMs = null;
        validPreviousState = false;
    }

    /// <summary>
    /// Patch to handle showing received items.
    /// Message shows for a fixed time, then goes on a short cooldown before showing the next message.
    /// The player must also be idle for a set amount of time (to avoid showing during cutscenes, dialogue, etc).
    /// </summary>
    [HarmonyPatch(typeof(InstructionPanel), nameof(InstructionPanel.Process))]
    public static void Prefix(InstructionPanel __instance)
    {
        // Check if InstructionPanel is off cooldown.
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (TextCooldownStartMs.HasValue && currentTime - TextCooldownStartMs > TextCooldownTimeMs)
        {
            TextCooldownStartMs = null;
        }

        // Check if InstructionPanel should be on cooldown.
        if (currentTime - TextShowStartMs > TextShowTimeMs)
        {
            TextShowStartMs = null;
            TextCooldownStartMs = currentTime;
            __instance.SetInstruction(null, true);
            return;
        }

        // Check that player is in valid state for long enough.
        if (!CanShowTextDuringState(Global.Director.player.state))
        {
            validPreviousState = false;
            return;
        }

        if (!validPreviousState)
        {
            validPreviousState = true;
            IdleStartMs = currentTime;
            return;
        }

        // Check that valid state is off cooldown.
        if (IdleStartMs.HasValue && currentTime - IdleStartMs > IdleStateMs)
        {
            IdleStartMs = null;
        }

        // Check that no cooldowns are active.
        if (TextShowStartMs.HasValue || TextCooldownStartMs.HasValue || IdleStartMs.HasValue)
        {
            return;
        }

        // Check that there are messages to show.
        if (Global.State.Messages.Count == 0)
        {
            return;
        }

        var text = Global.State.Messages.Dequeue();
        TextShowStartMs = currentTime;
        __instance.SetInstruction(text, true);
    }

    [HarmonyPatch(typeof(InstructionPanel), nameof(InstructionPanel.GetInstructionText))]
    public static bool Prefix(string id, ref string __result)
    {
        // If queued message should show, use the id as the text.
        if (TextShowStartMs != null)
        {
            __result = id;
            return false;
        }

        return true;
    }

    private static bool CanShowTextDuringState(ObjectPlayer.State state)
    {
        // Show text if player is not in cutscene, not in menu, and not in dialogue.
        return !InvalidStates.Contains(state) 
            && Global.Director.uiDialog == null
            && Global.Director.dialoguePanel?.IsOver() != false;
    }
}
