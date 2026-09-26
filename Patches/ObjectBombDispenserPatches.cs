using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using Object = Il2CppPipistrello.Object;

namespace PipistrelloArchipelago.Patches;

/// <summary>
/// Patches for handling bomb dispensers as Archipelago items.
/// </summary>
[HarmonyPatch]
internal static class ObjectBombDispenserPatches
{
    private static bool _isDeactivated;

    /// <summary>
    /// Disables bomb dispensers until Archipelago item is found.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
    private static void Director_InstantiateFromMap_Prefix(ref Mapvania.Object mapObj)
    {
        if (mapObj == null || mapObj.isDev || mapObj.objectDefName != "bombDispenser")
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

    /// <summary>
    /// Marks the bomb dispenser sprite for replacement.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectBombDispenser), nameof(ObjectBombDispenser.Draw))]
    private static void ObjectBombDispenser_Draw_Prefix(ObjectBombDispenser __instance)
    {
        if (__instance.specialState != Object.SpecialState.Deactivated)
        {
            return;
        }

        _isDeactivated = true;
        __instance.specialState = Object.SpecialState.None;
        __instance.animFrame = 1; // 2nd frame is fully closed.
    }

    /// <summary>
    /// Replaces the bomb dispenser sprite.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(SpriteManager), nameof(SpriteManager.GetSprite))]
    private static void SpriteManager_GetSprite_Prefix(ref string sprId)
    {
        if (_isDeactivated && sprId == "objs/bombDispenser")
        {
            sprId = Constants.BombDispenserDisabledSpriteName;
        }
    }

    /// <summary>
    /// Unmarks the bomb dispenser sprite for replacement.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectBombDispenser), nameof(ObjectBombDispenser.Draw))]
    private static void ObjectBombDispenser_Draw_Postfix(ObjectBombDispenser __instance)
    {
        if (!_isDeactivated)
        {
            return;
        }

        _isDeactivated = false;
        __instance.specialState = Object.SpecialState.Deactivated;
        __instance.animFrame = 0;
    }

    /// <summary>
    /// Prevents bombs from being held.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(ObjectBomb), nameof(ObjectBomb.Process))]
    private static void ObjectBomb_Process_Postfix(ObjectBomb __instance)
    {
        // Don't disable explosions, since those are still needed for traps and boss fights.
        if (!Global.Director.GetFlagBool("g:dev"))
        {
            __instance.canBeHeldState = false;
        }
    }
}
