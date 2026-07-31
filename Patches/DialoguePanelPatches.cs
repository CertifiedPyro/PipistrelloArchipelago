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
        // Check if player acquired a physical Archipelago item.
        if (Global.State.AcquiredPhysicalItem == null)
        {
            return true;
        }

        var item = Global.State.AcquiredPhysicalItem;
        if (!_showedArchItemDialogue)
        {
            var itemName = item.ItemDisplayName.Replace(" ", "[nbsp]");
            var playerName = item.Player.Name.Replace(" ", "[nbsp]");

            if (item.Player.Slot == Global.State.Session.ConnectionInfo.Slot)
            {
                text = $"[instant|You found your [c:blue|{itemName}]!][w:2]";
            }
            else
            {
                text = $"[instant|You sent [c:blue|{itemName}] to [c:red|{playerName}]!][w:2]";
            }

            _showedArchItemDialogue = true;
            return true;
        }
        else
        {
            // Don't show the remaining original dialogue, unless location was a taxi phone.
            var objectId = Utils.LocationIdToObjectId(item.LocationId);
            var mapObject = Utils.GetMapvaniaObject(objectId);
            return mapObject.objectDefName == "taxiPhone";
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
            Global.State.AcquiredPhysicalItem = null;
            _showedArchItemDialogue = false;
        }
    }
}
