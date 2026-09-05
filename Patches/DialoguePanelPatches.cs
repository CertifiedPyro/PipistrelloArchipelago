using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to override dialogue if physical Archipelago object is acquired.
/// </summary>
[HarmonyPatch]
internal static class DialoguePanelPatch
{
    private static bool _replacedDialogue;
    
    /// <summary>
    /// If loading save, reset internal state.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix()
    {
        _replacedDialogue = false;
    }
    
    /// <summary>
    /// Overrides dialogue text if physical Archipelago object is acquired.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    private static bool DialoguePanel_InjectText_Prefix(ref string text)
    {
        if (_replacedDialogue)
        {
            return Global.State.ShowRemainingDialogue;
        }

        if (Global.State.DialogueText == null)
        {
            return true;
        }
        
        // Replace the first message with the desired text.
        text = Global.State.DialogueText;
        _replacedDialogue = true;
        return true;
    }

    /// <summary>
    /// Handles cleanup after dialogue is over.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.IsOver))]
    private static void DialoguePanel_IsOver_Postfix(DialoguePanel __instance, bool __result)
    {
        if (__result && _replacedDialogue)
        {
            Global.State.DialogueText = null;
            _replacedDialogue = false;
        }
    }
}
