using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches for handling levers as Archipelago items.
/// </summary>
[HarmonyPatch]
internal static class ObjectLeverPatches
{
    private static bool _isDeactivated;
    private static string _spriteName;

    /// <summary>
    /// Disables levers until Archipelago item is found.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        if (mapObj == null || mapObj.isDev)
        {
            return;
        }

        if (mapObj.objectDefName == "lever" && mapObj.globalObjectId.objectId != "archResetLever")
        {
            // TODO: Check Archipelago item flag rather than g:dev flag.
            var flag = $"t:{mapObj.globalObjectId.AsString}:archDeactivated";
            var code = $$"""
                         const lever = id(\"{{mapObj.globalObjectId.objectId}}\")
                         const flagArch = flag(\"{{flag}}\")
                         if (!flagArch.isOn() && !flag(\"g:dev\").isOn())
                         {
                            wait(0.5)
                            lever.deactivateWithPoof()
                            flagArch.turnOn()
                         }
                         else if (flagArch.isOn() && flag(\"g:dev\").isOn())
                         {
                            lever.deactivateWithPoof()
                            lever.activate()
                            flagArch.turnOff()
                         }
                         """;
            var setupCode = new Mapvania.Object
            {
                objectDefId = "lor110",
                objectDefName = "setupCode",
                globalObjectId = new Game.GlobalObjectId
                {
                    mapId = mapObj.globalObjectId.mapId,
                    roomId = mapObj.globalObjectId.roomId,
                    objectId = mapObj.globalObjectId.objectId + "_archCode",
                },
                position = mapObj.position,
                width = mapObj.width,
                height = mapObj.height,
                properties = JsonValue.Parse($$"""{"mode": "runAlwaysOnAnyFlagChange", "code": "{{code}}"}"""),
                usesFlags = true,
            };
            Global.Director.InstantiateFromMap(setupCode);
        }
    }

    /// <summary>
    /// Marks the lever sprite for replacement.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectLever), nameof(ObjectLever.Draw))]
    private static void ObjectLever_Draw_Prefix(ObjectLever __instance)
    {
        if (__instance.mapObject.isDev || __instance.specialState != Object.SpecialState.Deactivated)
        {
            return;
        }

        _isDeactivated = true;
        __instance.specialState = Object.SpecialState.None;
        _spriteName = __instance.spriteName;
        __instance.spriteName = Constants.LeverDisabledSpriteName;
    }

    /// <summary>
    /// Replaces the lever sprite.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectLever), nameof(ObjectLever.Draw))]
    private static void ObjectLever_Draw_Postfix(ObjectLever __instance)
    {
        if (!_isDeactivated)
        {
            return;
        }

        _isDeactivated = false;
        __instance.specialState = Object.SpecialState.Deactivated;
        __instance.spriteName = _spriteName;
    }
}
