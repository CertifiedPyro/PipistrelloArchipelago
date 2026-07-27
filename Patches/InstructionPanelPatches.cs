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

    private static long? TextShowStartMs;
    private static long? TextCooldownStartMs;
    private static long? IdleStartMs;
    private static ObjectPlayer.State previousState;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch(int savefileIndex)
    {
        TextShowStartMs = null;
        TextCooldownStartMs = null;
        IdleStartMs = null;
        previousState = ObjectPlayer.State.Cutscene;
    }

    [HarmonyPatch(typeof(InstructionPanel), nameof(InstructionPanel.Process))]
    public static void Prefix(InstructionPanel __instance)
    {
        // Check if InstructionPanel should be on cooldown.
        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - TextShowStartMs > TextShowTimeMs)
        {
            TextShowStartMs = null;
            TextCooldownStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            __instance.SetInstruction(null, true);
        }

        // Check if InstructionPanel is off cooldown.
        if (TextCooldownStartMs.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - TextCooldownStartMs > TextCooldownTimeMs)
        {
            TextCooldownStartMs = null;
        }

        // Check that idle state is off cooldown.
        if (IdleStartMs.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - IdleStartMs > IdleStateMs)
        {
            IdleStartMs = null;
        }

        // Check that player is in idle state for long enough.
        if (GlobalState.Director.player.state != ObjectPlayer.State.Idle)
        {
            previousState = GlobalState.Director.player.state;
            return;
        }

        if (previousState != ObjectPlayer.State.Idle)
        {
            previousState = GlobalState.Director.player.state;
            IdleStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // Check that no cooldowns are active.
        if (TextShowStartMs.HasValue || TextCooldownStartMs.HasValue || IdleStartMs.HasValue)
        {
            return;
        }

        // Check if InstructionPanel should show due to acquired physical Archipelago item.
        // TODO: Check that InstructionPanel isn't already showing.
        if (SaveState.Messages.Count > 0)
        {
            var text = SaveState.Messages.Dequeue();
            TextShowStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            __instance.SetInstruction(text, true);
        }
    }

    [HarmonyPatch(typeof(InstructionPanel), nameof(InstructionPanel.GetInstructionText))]
    public static bool Prefix(string id, InstructionPanel __instance, ref string __result)
    {
        // If InstructionPanel should show due to acquired physical Archipelago item, return proper string.
        if (TextShowStartMs != null)
        {
            __result = id;
            return false;
        }

        return true;
    }
}
