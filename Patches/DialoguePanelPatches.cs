using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patch for replacing dialogue when a physical Archipelago item is picked up.
/// </summary>
[HarmonyPatch]
public class DialoguePanelPatch
{
    private static bool ShowedArchItemDialogue;
    private static int UnlockedTaxiPhones;

    /// <summary>
    /// If loading save, reset global state.
    /// </summary>
    [HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    [HarmonyPostfix]
    public static void InitFromSavefilePatch(int savefileIndex)
    {
        ShowedArchItemDialogue = false;
        UnlockedTaxiPhones = -1;
    }

    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.InjectText))]
    public static bool Prefix(DialoguePanel __instance, ref string text)
    {
        // Handle taxi phones (because Game.TaxiPhoneUnlock() isn't called for some reason).
        var currentTaxiPhoneCount = GlobalState.Director.playerRecord.taxiPhonesUnlocked.Count;
        if (UnlockedTaxiPhones == -1)
        {
            UnlockedTaxiPhones = currentTaxiPhoneCount;
        }

        if (currentTaxiPhoneCount > UnlockedTaxiPhones)
        {
            UnlockedTaxiPhones = currentTaxiPhoneCount;

            // Assume the player is interacting with a ObjectTaxiPhone now.
            // Find the closest ObjectTaxiPhone.
            Func<ObjectTaxiPhone, bool> predicate = (_) => true;
            var taxiPhoneObject = GlobalState.Director.FindNearestObject<ObjectTaxiPhone>(
                GlobalState.Director.player.position, predicate);
            Utils.SendLocationCheck(taxiPhoneObject.globalObjectId.AsString);
        }

        // Show player that they acquired a physical Archipelago item.
        if (SaveState.AcquiredPhysicalItem != null)
        {
            var item = SaveState.AcquiredPhysicalItem;
            if (!ShowedArchItemDialogue)
            {
                var alreadyChecked = SaveState.Session.Locations.AllLocationsChecked.Contains(item.LocationId);
                var itemName = item.ItemDisplayName.Replace(" ", "[nbsp]");
                var playerName = item.Player.Name.Replace(" ", "[nbsp]");

                if (item.Player.Slot == SaveState.Session.ConnectionInfo.Slot)
                {
                    text = $"[instant|You found your [c:blue|{itemName}]!][w:2]";
                }
                else
                {
                    text = $"[instant|You sent [c:blue|{itemName}] to [c:red|{playerName}]!][w:2]";
                }
                ShowedArchItemDialogue = true;
            }
            else
            {
                // Don't show the remaining original dialogue.
                return false;
            }
        }

        return true;
    }

    [HarmonyPatch(typeof(DialoguePanel), nameof(DialoguePanel.IsOver))]
    public static void Postfix(ref bool __result)
    {
        // Remove replaced text once dialogue is over.
        if (__result)
        {
            SaveState.AcquiredPhysicalItem = null;
            ShowedArchItemDialogue = false;
        }
    }
}
