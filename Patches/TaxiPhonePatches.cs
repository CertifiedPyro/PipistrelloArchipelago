using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;

namespace PipistrelloArchipelago.Patches;

[HarmonyPatch]
internal class TaxiPhonePatches
{
    [HarmonyPrefix, HarmonyPatch(typeof(Localization), nameof(Localization.GetEntries))]
    private static void Localization_GetEntries_Prefix(string stringId)
    {
        // Check if taxi phone dialogue is showing.
        if (stringId is not ("taxiPhone_unlock" or "taxiPhone_unlocked"))
        {
            return;
        }

        // Assume the player is interacting with a ObjectTaxiPhone now.
        // Find the closest ObjectTaxiPhone.
        Func<ObjectTaxiPhone, bool> predicate = _ => true;
        var taxiPhoneObject = Global.Director.FindNearestObject<ObjectTaxiPhone>(
            Global.Director.player.position, predicate);

        var globalObjectId = taxiPhoneObject.globalObjectId.AsString;
        if (!Utils.IsObjectIdActiveLocation(globalObjectId))
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
        var flag = $"{Game.GLOBAL_FLAG_PREFIX}{globalObjectId}{Constants.FlagInteractSuffix}";
        if (Global.Director.GetFlagBool(flag))
        {
            return;
        }

        Utils.SendLocationCheck(globalObjectId);
    }
}
