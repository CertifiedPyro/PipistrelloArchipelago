using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(PipistrelloArchipelago.Core), "PipistrelloArchipelago", "0.1.0", "CertifiedPyro", null)]
[assembly: MelonGame("Pocket Trap", "Pipistrello")]

namespace PipistrelloArchipelago;

[HarmonyLib.HarmonyPatch(typeof(Director), nameof(Director.Init))]
public class DirectorPatch
{
    public static Director instance;

    public static void Postfix(Director __instance)
    {
        instance = __instance;
        MelonLogger.Msg("Director Reference Captured!");
    }
}

[HarmonyLib.HarmonyPatch(typeof(ObjectPetalContainer))]
public class ObjectPetalContainerPatch
{
    public static ObjectPetalContainer instance;

    //[HarmonyLib.HarmonyPatch(nameof(ObjectPetalContainer.Draw))]
    //public static bool Prefix(ObjectPetalContainer __instance)
    //{
    //    instance = __instance;
    //    return false;
    //}

    // Isn't called??
    //[HarmonyLib.HarmonyPatch(nameof(ObjectPetalContainer.isCollectibleWithVisualEffects), HarmonyLib.MethodType.Getter)]
    //public static bool Prefix(ObjectPetalContainer __instance, ref bool __result)
    //{
    //    instance = __instance;
    //    __result = false;
    //    return false;
    //}

    // Isn't called??
    //[HarmonyLib.HarmonyPatch(nameof(ObjectPetalContainer.CheckForAcquisition))]
    //public static bool Prefix(ObjectPetalContainer __instance, ref bool __result)
    //{
    //    instance = __instance;
    //    __result = false;
    //    MelonLogger.Msg($"Making petal container {__instance.petalContainerId} not acquirable");
    //    return false;
    //}

    // Prevents pickup, but doesn't prevent it from being drawn if it exists, or not drawn if it doesn't exist
    // Isn't called if it doesn't exist
    //[HarmonyLib.HarmonyPatch(nameof(ObjectPetalContainer.Process))]
    //public static bool Prefix(ObjectPetalContainer __instance)
    //{
    //    if (__instance.petalContainerId == "city/ren223/yug5534")
    //    {
    //        instance = __instance;
    //        MelonLogger.Msg($"Calling petal container {__instance.petalContainerId} Process()");
    //    }

    //    return false;
    //}

    //[HarmonyLib.HarmonyPatch(nameof(ObjectPetalContainer.Register))]
    //public static void Prefix()
    //{
    //    MelonLogger.Msg("Caling ObjectPetalContainer.Register");
    //}
}

// Program flow??
//  - Director.InitRoom() - Once per room, even if visiting again
//  - ??
//  - Game.IsPetalContainerAcquired()
//  - Director.CreateObject() - For each object in room, even if visiting again?
//      - If petal container is acquired, CreateObject() is not called
// Note: petal containers not contained in director.room.objects (Mapvania.Room), but is called
// Note: petal containers not contained in director.currentProject.petalContainerMeta (Mapvania.Project), just contains all petal container coords, but is called

//[HarmonyLib.HarmonyPatch(typeof(Director))]
//public class DirectorPatchDebug
//{
//    public static Director instance;

//    [HarmonyLib.HarmonyPatch(nameof(Director.InitRoom))]
//    public static void Prefix(Director __instance)
//    {
//        instance = __instance;
//        if (instance.currentProject == null)
//        {
//            return;
//        }

//        MelonLogger.Msg(instance.room.id);
//        foreach (var petalContainerMeta in instance.currentProject.petalContainerMeta)
//        {
//            MelonLogger.Msg($"{petalContainerMeta.id} - {petalContainerMeta.room.id}");
//        }
//        MelonLogger.Msg("-------------------------------------------\n");
//    }
//}

//[HarmonyLib.HarmonyPatch(typeof(Game))]
//public class GamePatch
//{
//    [HarmonyLib.HarmonyPatch(nameof(Game.IsPetalContainerAcquired))]
//    public static void Prefix(string id)
//    {
//        MelonLogger.Msg($"IsPetalContainerAcquired(): {id}");

//        StackTrace stackTrace = new StackTrace(true);
//        StackFrame[] frames = stackTrace.GetFrames();
//        for (int i = 1; i < frames.Length; i++)
//        {
//            MethodBase method = frames[i].GetMethod();
//            if (method == null) continue;

//            // Log the class and method name
//            MelonLogger.Msg($"{i}: {method.DeclaringType?.Name}.{method.Name}");
//        }
//    }
//}

public class Core : MelonMod
{
    private static KeyCode addKey = KeyCode.P;
    private static KeyCode removeKey = KeyCode.O;

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Initialized.");
    }

    public override void OnLateUpdate()
    {
        // Health (petal container)
        if (Input.GetKeyDown(addKey) && Input.GetKey(KeyCode.LeftControl))
        {
            LoggerInstance.Msg("Adding petal container!");

            var director = DirectorPatch.instance;
            var petalContainerName = Game.GetPetalContainerName(0);
            //Game.SetPetalContainerAcquired(director, "test123", 1, true);
            Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, true);

            LoggerInstance.Msg("Finished adding petal container!");
        }
        else if (Input.GetKeyDown(removeKey) && Input.GetKey(KeyCode.LeftControl))
        {
            LoggerInstance.Msg("Removing petal container!");

            var director = DirectorPatch.instance;
            var petalContainerName = Game.GetPetalContainerName(0);
            //Game.SetPetalContainerAcquired(director, "test123", 1, false);
            Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, false);

            LoggerInstance.Msg("Finished removing petal container!");
        }
        // BP (BP container)

        // Upgrades
        else if (Input.GetKeyDown(addKey) && Input.GetKey(KeyCode.LeftShift))
        {
            LoggerInstance.Msg("Adding upgrade!");

            var director = DirectorPatch.instance;
            var upgrade = Game.GetUpgradeById("yoyoBounceAttackUp");
            Game.SetUpgradeAcquired(director, upgrade, true);

            LoggerInstance.Msg("Finished adding upgrade!");
        }
        else if (Input.GetKeyDown(removeKey) && Input.GetKey(KeyCode.LeftShift))
        {
            LoggerInstance.Msg("Removing upgrade!");

            var director = DirectorPatch.instance;
            var upgrade = Game.GetUpgradeById("yoyoBounceAttackUp");
            Game.SetUpgradeAcquired(director, upgrade, false);

            LoggerInstance.Msg("Finished removing upgrade!");
        }
        // Badges
        else if (Input.GetKeyDown(addKey))
        {
            LoggerInstance.Msg("Adding badge!");

            var director = DirectorPatch.instance;
            var equip = Game.GetEquipById("walkTheDogFireTrail");
            Game.SetEquipAcquired(director, equip, true, true);

            LoggerInstance.Msg("Finished adding badge!");
        }
        else if (Input.GetKeyDown(removeKey))
        {
            LoggerInstance.Msg("Removing badge!");

            var director = DirectorPatch.instance;
            var equip = Game.GetEquipById("walkTheDogFireTrail");
            Game.SetEquipAcquired(director, equip, false, true);

            LoggerInstance.Msg("Finished removing badge!");
        }
    }
}
