using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches for handling buoys as Archipelago items.
/// </summary>
[HarmonyPatch]
internal static class ObjectBuoyPatches
{
    private static bool _isDeactivated;

    /// <summary>
    /// Disables buoys until Archipelago item is found.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        if (mapObj == null || mapObj.isDev || mapObj.objectDefName != "lifebuoy")
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
                        wait(0.5)
                        obj.deactivateWithPoof()
                        flagArch.turnOn()
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
    }

    /// <summary>
    /// Marks the buoy sprite for replacement.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectLifebuoy), nameof(ObjectLifebuoy.Draw))]
    private static void ObjectLifebuoy_Draw_Prefix(ObjectLifebuoy __instance)
    {
        if (__instance.specialState != Object.SpecialState.Deactivated)
        {
            return;
        }

        _isDeactivated = true;
        __instance.specialState = Object.SpecialState.None;
    }

    /// <summary>
    /// Replaces the buoy sprite.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Prefix(ref string sprId)
    {
        if (_isDeactivated && sprId == "objs/lifebuoy")
        {
            sprId = Constants.DisabledBuoySpriteName;
        }
    }

    /// <summary>
    /// Unmarks the buoy sprite for replacement.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectLifebuoy), nameof(ObjectLifebuoy.Draw))]
    private static void ObjectLifebuoy_Draw_Postfix(ObjectLifebuoy __instance)
    {
        if (!_isDeactivated)
        {
            return;
        }

        _isDeactivated = false;
        __instance.specialState = Object.SpecialState.Deactivated;
    }
}
