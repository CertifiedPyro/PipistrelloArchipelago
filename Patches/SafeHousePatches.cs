using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using UnityEngine;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches to modify the safe house.
/// </summary>
[HarmonyPatch]
internal static class SafeHousePatches
{
    private const string LeverObjectId = "archResetLever";
    private const string SignObjectId = "archResetSign";
    private const string ResetFlag = "t:archResetExit";

    private static Game.GlobalObjectId _originalSafeHouseExitId;

    /// <summary>
    /// If loading save, reset internal state.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix()
    {
        _originalSafeHouseExitId = null;
    }

    /// <summary>
    /// Adds the lever that resets to South Plaza.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.LoadProject))]
    private static void Director_LoadProject_Postfix()
    {
        // Add lever to Safe House that resets to South Plaza.
        var map = Global.Director.currentProject.maps.ToArray().FirstOrDefault(m => m.id == "safehouse")!;
        var room = map.rooms.ToArray().FirstOrDefault(r => r.id == "mig38")!;
        var objects = room.objects;
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == LeverObjectId) == null)
        {
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "lor20",
                    objectDefName = "lever",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "safehouse",
                        roomId = "mig38",
                        objectId = LeverObjectId
                    },
                    position = new Vector2(9 * 16, 8 * 16),
                    width = 16,
                    height = 16,
                    properties = JsonValue.Parse($"{{\"controlsFlag\": \"{ResetFlag}\", \"mode\": \"toggle\"}}"),
                    usesFlags = true
                });
        }

        // Add sign that explains the lever.
        if (objects.ToArray().FirstOrDefault(o => o.globalObjectId.objectId == SignObjectId) == null)
        {
            const string signText = "Use this lever to return to South Plaza (if you are soft-locked).";
            room.objects.Add(
                new Mapvania.Object
                {
                    objectDefId = "mig589",
                    objectDefName = "sign",
                    globalObjectId = new Game.GlobalObjectId
                    {
                        mapId = "safehouse",
                        roomId = "mig38",
                        objectId = SignObjectId
                    },
                    position = new Vector2(8 * 16, 8 * 16),
                    width = 16,
                    height = 16,
                    properties = JsonValue.Parse($$"""{"code": "this.say(\"{{signText}}\")", "hideShadow": true}"""),
                    usesFlags = true
                });
        }
    }

    /// <summary>
    /// Handles lever state change.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.SetFlagBool))]
    private static void Director_SetFlagBool_Postfix(string flag, bool value)
    {
        if (flag != ResetFlag)
        {
            return;
        }

        if (value)
        {
            _originalSafeHouseExitId = Global.Director.FindSafehouseExit().globalObjectId;
            Global.Director.playerRecord.safehouseExitId = new Il2CppSystem.Nullable<Game.GlobalObjectId>(
                new Game.GlobalObjectId
                {
                    mapId = "city",
                    roomId = "ren223",
                    objectId = "lor366"
                });
            Global.State.Messages.Enqueue("[c:green|Reset enabled]: Safe House exit set to South Plaza.");
        }
        else
        {
            Global.Director.playerRecord.safehouseExitId =
                new Il2CppSystem.Nullable<Game.GlobalObjectId>(_originalSafeHouseExitId);
            Global.State.Messages.Enqueue("[c:red|Reset disabled]: Safe House exit set back to original.");
        }
    }
}
