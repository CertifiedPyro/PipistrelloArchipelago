using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to override dialogue if physical Archipelago object is acquired.
/// </summary>
[HarmonyPatch]
internal class DialoguePanelPatch
{
    private const string ArchDialogueShown = "ARCH_DIALOGUE_SHOWN";

    /// <summary>
    /// Overrides dialogue text if physical Archipelago object is acquired.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    private static bool DialoguePanel_InjectText_Prefix(ref string text)
    {
        switch (Global.State.DialogueText)
        {
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
    /// Handles cleanup after dialogue is over.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.IsOver))]
    private static void DialoguePanel_IsOver_Postfix(bool __result)
    {
        if (__result)
        {
            Global.State.DialogueText = null;
        }
    }
}
