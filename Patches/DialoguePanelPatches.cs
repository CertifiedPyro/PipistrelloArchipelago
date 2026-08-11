using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patch for replacing dialogue when a physical Archipelago item is picked up.
/// </summary>
[HarmonyPatch]
public class DialoguePanelPatch
{
    private const string _ARCH_DIALOGUE_SHOWN = "ARCH_DIALOGUE_SHOWN";

    /// <summary>
    /// Patch to handle overwriting dialogue text if physical Archipelago item is picked up.
    /// </summary>
    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    [HarmonyPrefix]
    public static bool InjectTextPatch(ref string text)
    {
        // Check if there is Archipelago dialogue to show.
        if (Global.State.DialogueText == null)
        {
            return true;
        }

        if (Global.State.DialogueText == _ARCH_DIALOGUE_SHOWN)
        {
            return Global.State.ShowRemainingDialogue;
        }

        // Replace the first message with the desired text.
        text = Global.State.DialogueText;
        Global.State.DialogueText = _ARCH_DIALOGUE_SHOWN;
        return true;
    }

    /// <summary>
    /// Patch to handle when dialogue is finished.
    /// </summary>
    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.IsOver))]
    [HarmonyPostfix]
    public static void IsOverPatch(bool __result)
    {
        // Reset state once dialogue is over.
        if (__result)
        {
            Global.State.DialogueText = null;
        }
    }
}
