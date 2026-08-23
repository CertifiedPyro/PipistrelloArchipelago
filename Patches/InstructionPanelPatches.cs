using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patch for dialogue when a virtual Archipelago item is received.
/// </summary>
[HarmonyPatch]
public static class InstructionPanelPatch
{
    private const long TextShowTimeMs = 2500;
    private const long TextCooldownTimeMs = 250;
    private const long IdleStateMs = 750;

    private static readonly HashSet<ObjectPlayer.State> InvalidStates =
    [
        ObjectPlayer.State.AcquiringItem,
        ObjectPlayer.State.AcquiringMegaBattery,
        ObjectPlayer.State.AuntieFinish,
        ObjectPlayer.State.AuntieTalk,
        ObjectPlayer.State.Cutscene
    ];

    private static long? _textShowStartMs;
    private static long? _textCooldownStartMs;
    private static long? _idleStartMs;
    private static bool _validPreviousState;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch()
    {
        _textShowStartMs = null;
        _textCooldownStartMs = null;
        _idleStartMs = null;
        _validPreviousState = false;
    }

    /// <summary>
    /// Patch to handle showing received items.
    /// Message shows for a fixed time, then goes on a short cooldown before showing the next message.
    /// The player must also be idle for a set amount of time (to avoid showing during cutscenes, dialogue, etc).
    /// </summary>
    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.Process))]
    public static void Prefix(DialoguePanel __instance)
    {
        // // Check if InstructionPanel is off cooldown.
        // var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // if (_textCooldownStartMs.HasValue && currentTime - _textCooldownStartMs > TextCooldownTimeMs)
        // {
        //     _textCooldownStartMs = null;
        // }
        //
        // // Check if InstructionPanel should be on cooldown.
        // if (currentTime - _textShowStartMs > TextShowTimeMs)
        // {
        //     _textShowStartMs = null;
        //     _textCooldownStartMs = currentTime;
        //     // __instance.SetInstruction(null, true);
        //     return;
        // }
        //
        // // Check that player is in a valid state.
        // if (!CanShowTextDuringState(Global.Director.player.state))
        // {
        //     _validPreviousState = false;
        //     return;
        // }
        //
        // if (!_validPreviousState)
        // {
        //     _validPreviousState = true;
        //     _idleStartMs = currentTime;
        //     return;
        // }
        //
        // // Check that player is in a valid state for long enough.
        // if (_idleStartMs.HasValue && currentTime - _idleStartMs > IdleStateMs)
        // {
        //     _idleStartMs = null;
        // }
        //
        // // Check that no cooldowns are active.
        // if (_textShowStartMs.HasValue || _textCooldownStartMs.HasValue || _idleStartMs.HasValue)
        // {
        //     return;
        // }
        //
        // // Check that there are messages to show.
        // if (Global.State.Messages.IsEmpty)
        // {
        //     return;
        // }
        //
        // if (Global.State.Messages.TryDequeue(out var text))
        // {
        //     _textShowStartMs = currentTime;
        //     // __instance.InjectText(Global.Director.player, text);
        // }

        // if (__instance.currentTextScroll < __instance.textScrolls.Count &&
        //     __instance.textScrolls[__instance.currentTextScroll] is var currentTextScroll &&
        //     currentTextScroll.isWaitingClick)
        // {
        //     MelonLogger.Msg(
        //         $"{currentTextScroll.advanceTimer}, {currentTextScroll.ended}, {currentTextScroll.IsTalking()}, {currentTextScroll.IsOver()}");
        //     currentTextScroll.AcceptClick();
        // }
    }

    private static bool CanShowTextDuringState(ObjectPlayer.State state)
    {
        // Show text if player is not in cutscene, not in menu, not in dialogue, and is not dead.
        return !InvalidStates.Contains(state)
               && Global.Director.uiDialog == null
               && Global.Director.dialoguePanel?.IsOver() != false
               && !Global.Director.IsPlayerDead();
    }
}