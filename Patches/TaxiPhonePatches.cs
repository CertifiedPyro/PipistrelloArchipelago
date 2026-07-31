using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
public class TaxiPhonePatches
{
    [HarmonyPatch(typeof(Localization), nameof(Localization.GetEntries))]
    [HarmonyPrefix]
    public static void LocalizationPatch(string stringId)
    {
        // Check if taxi phone dialogue is showing.
        if (stringId == "taxiPhone_unlock")
        {
            // Assume the player is interacting with a ObjectTaxiPhone now.
            // Find the closest ObjectTaxiPhone.
            Func<ObjectTaxiPhone, bool> predicate = (_) => true;
            var taxiPhoneObject = Global.Director.FindNearestObject<ObjectTaxiPhone>(
                Global.Director.player.position, predicate);

            if (!Utils.IsObjectIdActiveLocation(taxiPhoneObject.globalObjectId.AsString))
            {
                return;
            }

            // Sanity check that the player is close to the taxi phone.
            if (Global.Director.currentRoomId != taxiPhoneObject.globalObjectId.roomId)
            {
                Melon<PipArchMod>.Logger.Msg("Could not find correct taxi phone for location check.");
                return;
            }

            // Check if player already interacted with this taxi phone.
            var flag = $"{Game.GLOBAL_FLAG_PREFIX}{taxiPhoneObject.globalObjectId.AsString}{Constants.FLAG_INTERACT_SUFFIX}";
            if (Global.Director.GetFlagBool(flag))
            {
                return;
            }

            Utils.SendLocationCheck(taxiPhoneObject.globalObjectId.AsString);
        }
    }
}
