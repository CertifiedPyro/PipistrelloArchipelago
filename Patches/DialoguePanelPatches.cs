using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patch for replacing dialogue when a physical Archipelago item is picked up.
/// </summary>
[HarmonyPatch]
internal class DialoguePanelPatch
{
    private const string ArchDialogueShown = "ARCH_DIALOGUE_SHOWN";

    /// <summary>
    /// Patch to handle overwriting dialogue text if physical Archipelago item is picked up.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    private static bool DialoguePanel_InjectText_Prefix(ref string text)
    {
        switch (Global.State.DialogueText)
        {
            // Check if there is Archipelago dialogue to show.
            case null:
                return true;
            case ArchDialogueShown:
                return Global.State.ShowRemainingDialogue;
            default:
                // Replace the first message with the desired text.
                text = Global.State.DialogueText;
                Global.State.DialogueText = ArchDialogueShown;
                return true;
        }
    }

    /// <summary>
    /// Patch to handle when dialogue is finished.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.IsOver))]
    private static void DialoguePanel_IsOver_Postfix(bool __result)
    {
        // Reset state once dialogue is over.
        if (__result)
        {
            Global.State.DialogueText = null;
        }
    }
}
