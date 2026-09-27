using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches for handling hooks as Archipelago items.
/// </summary>
[HarmonyPatch]
internal static class ObjectHookPatches
{
    private static bool _isDeactivated;

    /// <summary>
    /// Disables hooks until Archipelago item is found.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        if (mapObj == null || mapObj.isDev || mapObj.objectDefName != "hook")
        {
            return;
        }

        // TODO: Check Archipelago item flag rather than g:dev flag.
        var flag = $"t:{mapObj.globalObjectId.AsString}:archDeactivated";
        var code = $$"""
                     const obj = id(\"{{mapObj.globalObjectId.objectId}}\")
                     const flagArch = flag(\"{{flag}}\")
                     if (!flagArch.isOn() && !flag(\"g:dev\").isOn())
                     {
                        obj.deactivate()
                        flagArch.turnOn()
                        wait(0.75)
                        obj.deactivateWithPoof()
                     }
                     else if (flagArch.isOn() && flag(\"g:dev\").isOn())
                     {
                        obj.deactivateWithPoof()
                        obj.activate()
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

        // Set prescence flag, since deactivation doesn't completely disable the hook.
        var field = mapObj.properties.Fields.ToArray().FirstOrDefault(f => f.name == "presenceFlag");
        if (field != null)
        {
            mapObj.properties.Fields.Remove(field);
        }

        mapObj.properties.SetField(
            "presenceFlag",
            $"!{flag}");
    }

    /// <summary>
    /// Marks the hook sprite for replacement.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectHook), nameof(ObjectHook.Draw))]
    private static void ObjectHook_Draw_Prefix(ObjectHook __instance)
    {
        if (__instance.specialState != Object.SpecialState.Deactivated)
        {
            return;
        }

        _isDeactivated = true;
        __instance.specialState = Object.SpecialState.None;
    }

    /// <summary>
    /// Replaces the hook sprite.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Prefix(ref string sprId)
    {
        if (_isDeactivated && sprId == "objs/hook")
        {
            sprId = Constants.DisabledHookSpriteName;
        }
    }

    /// <summary>
    /// Unmarks the hook sprite for replacement.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectHook), nameof(ObjectHook.Draw))]
    private static void ObjectHook_Draw_Postfix(ObjectHook __instance)
    {
        if (!_isDeactivated)
        {
            return;
        }

        _isDeactivated = false;
        __instance.specialState = Object.SpecialState.Deactivated;
    }
}
