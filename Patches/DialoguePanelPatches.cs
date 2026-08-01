using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patch for replacing dialogue when a physical Archipelago item is picked up.
/// </summary>
[HarmonyPatch]
public class DialoguePanelPatch
{
    private static bool _showedArchItemDialogue;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch(int savefileIndex)
    {
        _showedArchItemDialogue = false;
    }

    /// <summary>
    /// Patch to handle overwriting dialogue text if physical Archipelago item is picked up.
    /// </summary>
    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    public static bool Prefix(ref string text)
    {
        // Check if there is Archipelago dialogue to show.
        if (Global.State.DialogueText == null)
        {
            return true;
        }

        if (!_showedArchItemDialogue)
        {
            text = Global.State.DialogueText;
            _showedArchItemDialogue = true;
            return true;
        }
        else
        {
            return Global.State.ShowRemainingDialogue;
        }
    }

    /// <summary>
    /// Patch to handle when dialogue is finished.
    /// </summary>
    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.IsOver))]
    public static void Postfix(bool __result)
    {
        // Reset state once dialogue is over.
        if (__result)
        {
            Global.State.DialogueText = null;
            _showedArchItemDialogue = false;
        }
    }
}
