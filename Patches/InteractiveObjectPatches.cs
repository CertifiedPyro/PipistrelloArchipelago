using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches for handling interactive objects that can be Archipelago items.
/// </summary>
[HarmonyPatch]
internal static class InteractiveObjectPatches
{
    private static bool _leverIsDeactivated;
    private static string _leverSpriteName;

    /// <summary>
    /// Disables levers until Archipelago item is found.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        if (mapObj.isDev)
        {
            return;
        }

        if (mapObj?.objectDefName != "lever")
        {
            return;
        }

        // TODO: Check Archipelago item flag rather than g:dev flag.
        var flag = $"t:{mapObj.globalObjectId.AsString}:archDeactivated";
        var code = $$"""
                     const lever = id(\"{{mapObj.globalObjectId.objectId}}\")
                     const flagArch = flag(\"{{flag}}\")
                     if (!flagArch.isOn() && !flag(\"g:dev\").isOn())
                     {
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
                objectId = mapObj.globalObjectId.objectId + "_archCode"
            },
            position = mapObj.position,
            width = mapObj.width,
            height = mapObj.height,
            properties = JsonValue.Parse($$"""{"mode": "runAlwaysOnAnyFlagChange", "code": "{{code}}"}"""),
            usesFlags = true
        };
        Global.Director.InstantiateFromMap(setupCode);
    }

    [HarmonyPrefix, HarmonyPatch(typeof(ObjectLever), nameof(ObjectLever.Draw))]
    private static void ObjectLever_Draw_Prefix(ObjectLever __instance)
    {
        if (!__instance.mapObject.isDev && __instance.specialState == Object.SpecialState.Deactivated)
        {
            _leverIsDeactivated = true;
            __instance.specialState = Object.SpecialState.None;
            _leverSpriteName = __instance.spriteName;
            __instance.spriteName = Constants.LeverDisabledSpriteName;
        }
    }
    
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectLever), nameof(ObjectLever.Draw))]
    private static void ObjectLever_Draw_Postfix(ObjectLever __instance)
    {
        if (_leverIsDeactivated)
        {
            _leverIsDeactivated = false;
            __instance.specialState = Object.SpecialState.Deactivated;
            __instance.spriteName = _leverSpriteName;
        }
    }
}
