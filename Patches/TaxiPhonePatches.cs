using HarmonyLib;
using Il2CppPipistrello;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to handle taxi phones as Archipelago locations.
/// </summary>
[HarmonyPatch]
internal class TaxiPhonePatches
{
    /// <summary>
    /// Handles interaction with a taxi phone.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectTaxiPhone), nameof(ObjectTaxiPhone.OnInteract))]
    private static void ObjectTaxiPhone_OnInteract_Prefix(ObjectTaxiPhone __instance)
    {
        // Check that the object is eligible to be a location.
        var globalObjectId = __instance.globalObjectId.AsString;
        if (!Utils.IsObjectIdActiveLocation(globalObjectId))
        {
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
