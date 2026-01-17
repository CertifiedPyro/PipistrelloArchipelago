using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(PipistrelloArchipelago.Core), "PipistrelloArchipelago", "0.1.0", "CertifiedPyro", null)]
[assembly: MelonGame("Pocket Trap", "Pipistrello")]

namespace PipistrelloArchipelago;

public static class GlobalState
{
    public static bool RunningInitRoom = false;
    public static bool RunningCalculateIsHousePuzzleCompleted = false;
    public static bool RunningPetalContainerMenu = false;
    public static bool RunningBpContainerMenu = false;

    public static UIDialog CurrentPetalContainerMenu;
    public static UIDialog CurrentBpContainerMenu;

    public static bool AcquiringPhysicalPetalContainer = false;  // True while obtaining physical petal container
    public static bool AcquiringVirtualPetalContainer = false;  // True while Archipelago server gives us the petal container

    public static bool AcquiringPhysicalBpContainer = false;  // True while obtaining physical BP container
    public static bool AcquiringVirtualBpContainer = false;  // True while Archipelago server gives us the BP container
    public static bool IsPhysicalBpContainerAcquired = false;  // True if physical BP container is gone

    public static bool AcquiringPhysicalBadge = false;  // True while obtaining physical badge
    public static bool AcquiringVirtualBadge = false;  // True while Archipelago server gives us the badge
    public static bool IsPhysicalBadgeAcquired = false;  // True if physical badge is gone
}

[HarmonyPatch(typeof(Director))]
public class DirectorPatch
{
    public static Director instance;

    [HarmonyPatch(nameof(Director.Init))]
    public static void Postfix(Director __instance)
    {
        instance = __instance;
    }

    [HarmonyPatch(nameof(Director.InitRoom))]
    public static void Prefix()
    {
        GlobalState.RunningInitRoom = true;
    }

    [HarmonyPatch(nameof(Director.InitRoom))]
    public static void Postfix()
    {
        GlobalState.RunningInitRoom = false;
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.GetFlagBool))]
public class GameGetFlagBoolPatch
{
    public static void Postfix(string flag, ref bool __result)
    {
        // If original flag is true, then no need to check physical/virtual flags.
        if (__result)
        {
            return;
        }

        var basePhysicalPatch = GlobalState.RunningInitRoom || GlobalState.RunningCalculateIsHousePuzzleCompleted;
        var petalPhysicalPatch = basePhysicalPatch || GlobalState.RunningPetalContainerMenu;
        if (Utils.GetPetalIdFromFlag(flag, out var petalId) && petalId == "city/ren223/yug5534")
        {
            if (petalPhysicalPatch)
            {
                var physicalFlag = Utils.GetPetalPhysicalFlag(petalId);
                __result = DirectorPatch.instance.GetFlagBool(physicalFlag);
                MelonLogger.Msg($"Getting {physicalFlag}: {__result}");
                return;
            }
            else
            {
                var virtualFlag = Utils.GetPetalVirtualFlag(petalId);
                __result = DirectorPatch.instance.GetFlagBool(virtualFlag);
                MelonLogger.Msg($"Getting {virtualFlag}: {__result}");
                return;
            }
        }
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.SetFlagBool))]
public class GameSetFlagBoolPatch
{
    public static bool Prefix(string flag, ref bool value)
    {
        if (Utils.GetPetalIdFromFlag(flag, out var petalId) && petalId == "city/ren223/yug5534")
        {
            var physicalFlag = Utils.GetPetalPhysicalFlag(petalId);
            var virtualFlag = Utils.GetPetalVirtualFlag(petalId);
            if (GlobalState.AcquiringPhysicalPetalContainer)
            {
                DirectorPatch.instance.SetFlagBool(physicalFlag, value);
                MelonLogger.Msg($"Setting {physicalFlag}: {value}");
            }
            if (GlobalState.AcquiringVirtualPetalContainer)
            {
                DirectorPatch.instance.SetFlagBool(virtualFlag, value);
                MelonLogger.Msg($"Setting {virtualFlag}: {value}");
            }

            // Only set original acquired flag if physical and virtual flags are true.
            value = DirectorPatch.instance.GetFlagBool(physicalFlag) && DirectorPatch.instance.GetFlagBool(virtualFlag);
            MelonLogger.Msg($"Setting {flag}: {value}");
            return true;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Game))]
public class GamePatch
{
    // Runs as petal container is picked up (e.g. before text shows up)
    [HarmonyPatch(nameof(Game.SetPetalContainerAcquired))]
    [HarmonyPrefix]
    public static bool PrefixSetPetalContainerAcquired(string id, bool acquired, ref bool __result)
    {
        // If we're acquiring the petal container from Archipelago server, allow the original method to run.
        if (GlobalState.AcquiringVirtualPetalContainer)
        {
            return true;
        }

        // Otherwise, hijack the logic to not actually acquire the petal container.
        GlobalState.AcquiringPhysicalPetalContainer = true;
        DirectorPatch.instance.SetFlagBool(Game.FlagPetalContainerAcquired(id), acquired);
        // Only set if acquired=true, since it's set to false when dialogue panel closes.
        GlobalState.AcquiringPhysicalPetalContainer = acquired;

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(DialoguePanel))]
public class DialoguePanelPatch
{
    // DialoguePanel InjectText(): [instant|You got a [c:rose|Petal Container]!][w:3]
    // DialoguePanel InjectText(): [c:rose|Petals] collected so far: [c:blue|1].[d]\n Collect 8 to increase your [c|maximum life points]!  
    [HarmonyPatch(nameof(DialoguePanel.InjectText))]
    public static void Prefix(ref string text)
    {
        if (GlobalState.AcquiringPhysicalPetalContainer)
        {
            //MelonLogger.Msg($"DialoguePanel InjectText() original args: {text}");
            text = "[instant|You got a [c:blue|Archipelago item]!][w:2]";
        }
    }
 
    // Remove replace text once dialogue is over.
    [HarmonyPatch(nameof(DialoguePanel.IsOver))]
    public static void Postfix(ref bool __result)
    {
        if (__result)
        {
            if (GlobalState.AcquiringPhysicalPetalContainer)
            {
                GlobalState.AcquiringPhysicalPetalContainer = false;
            }
        }
    }
}

[HarmonyPatch(typeof(ObjectWarpArea))]
public class ObjectWarpAreaPatch
{
    [HarmonyPatch(nameof(ObjectWarpArea.CalculateIsHousePuzzleCompleted))]
    public static void Prefix()
    {
        GlobalState.RunningCalculateIsHousePuzzleCompleted = true;
    }

    [HarmonyPatch(nameof(ObjectWarpArea.CalculateIsHousePuzzleCompleted))]
    public static void Postfix()
    {
        GlobalState.RunningCalculateIsHousePuzzleCompleted = false;
    }
}

[HarmonyPatch(typeof(Menu))]
public class MenuPatch
{
    [HarmonyPatch(nameof(Menu.MakePetalContainerMenu))]
    [HarmonyPrefix]
    public static void PrefixPetalContainerMenu()
    {
        GlobalState.RunningPetalContainerMenu = true;
    }

    [HarmonyPatch(nameof(Menu.MakePetalContainerMenu))]
    public static void PostfixPetalContainerMenu(ref UIDialog __result)
    {
        GlobalState.CurrentPetalContainerMenu = __result;
    }

    [HarmonyPatch(nameof(Menu.MakeBpContainerMenu))]
    public static void PrefixBpContainerMenu()
    {
        GlobalState.RunningBpContainerMenu = true;
    }

    [HarmonyPatch(nameof(Menu.MakeBpContainerMenu))]
    public static void PostfixBpContainerMenu(ref UIDialog __result)
    {
        GlobalState.CurrentBpContainerMenu = __result;
    }
}

[HarmonyPatch(typeof(UIDialog))]
public class UIDialogPatch
{
    [HarmonyPatch(nameof(UIDialog.Close))]
    public static void Postfix(UIDialog __instance)
    {
        if (__instance == GlobalState.CurrentPetalContainerMenu)
        {
            GlobalState.RunningPetalContainerMenu = false;
            GlobalState.CurrentPetalContainerMenu = null;
        }
        else if (__instance == GlobalState.CurrentBpContainerMenu)
        {
            GlobalState.RunningBpContainerMenu = false;
            GlobalState.CurrentBpContainerMenu = null;
        }
    }
}

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
            LoggerInstance.Msg("Adding virtual petal container!");

            var director = DirectorPatch.instance;
            GlobalState.AcquiringVirtualPetalContainer = true;
            Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, true);
            GlobalState.AcquiringVirtualPetalContainer = false;

            LoggerInstance.Msg("Finished adding virtual petal container!");
        }
        else if (Input.GetKeyDown(removeKey) && Input.GetKey(KeyCode.LeftControl))
        {
            LoggerInstance.Msg("Removing petal container!");

            var director = DirectorPatch.instance;
            GlobalState.AcquiringVirtualPetalContainer = true;
            Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, false);
            GlobalState.AcquiringVirtualPetalContainer = false;
            Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, false);

            LoggerInstance.Msg("Finished removing petal container!");
        }
        //// BP (BP container)
        //else if (Input.GetKeyDown(addKey) && Input.GetKey(KeyCode.LeftAlt))
        //{
        //    LoggerInstance.Msg("Adding virtual BP container!");

        //    var director = DirectorPatch.instance;
        //    GlobalState.AcquiringVirtualBpContainer = true;
        //    Game.SetBpContainerAcquired(director, "city/yug5154/yug5202", 1, true);
        //    GlobalState.AcquiringVirtualBpContainer = false;

        //    LoggerInstance.Msg("Finished adding virtual BP container!");
        //}
        //else if (Input.GetKeyDown(removeKey) && Input.GetKey(KeyCode.LeftAlt))
        //{
        //    LoggerInstance.Msg("Removing BP container!");

        //    var director = DirectorPatch.instance;
        //    GlobalState.AcquiringVirtualBpContainer = true;
        //    Game.SetBpContainerAcquired(director, "city/yug5154/yug5202", 1, false);
        //    GlobalState.AcquiringVirtualBpContainer = false;
        //    GlobalState.IsPhysicalBpContainerAcquired = false;
        //    // TODO: Remove flag?

        //    LoggerInstance.Msg("Finished removing BP container!");
        //}
        //// Upgrades
        //else if (Input.GetKeyDown(addKey) && Input.GetKey(KeyCode.LeftShift))
        //{
        //    LoggerInstance.Msg("Adding upgrade!");

        //    var director = DirectorPatch.instance;
        //    var upgrade = Game.GetUpgradeById("yoyoBounceAttackUp");
        //    Game.SetUpgradeAcquired(director, upgrade, true);

        //    LoggerInstance.Msg("Finished adding upgrade!");
        //}
        //else if (Input.GetKeyDown(removeKey) && Input.GetKey(KeyCode.LeftShift))
        //{
        //    LoggerInstance.Msg("Removing upgrade!");

        //    var director = DirectorPatch.instance;
        //    var upgrade = Game.GetUpgradeById("yoyoBounceAttackUp");
        //    Game.SetUpgradeAcquired(director, upgrade, false);

        //    LoggerInstance.Msg("Finished removing upgrade!");
        //}
        //// Badges
        //else if (Input.GetKeyDown(addKey))
        //{
        //    LoggerInstance.Msg("Adding virtual badge!");

        //    var director = DirectorPatch.instance;
        //    GlobalState.AcquiringVirtualBadge = true;
        //    var equip = Game.GetEquipById("stringedYoyoNoPierce");
        //    Game.SetEquipAcquired(director, equip, true, true);
        //    GlobalState.AcquiringVirtualBadge = false;

        //    LoggerInstance.Msg("Finished adding virtual badge!");
        //}
        //else if (Input.GetKeyDown(removeKey))
        //{
        //    LoggerInstance.Msg("Removing badge!");

        //    var director = DirectorPatch.instance;
        //    GlobalState.AcquiringVirtualBadge = true;
        //    var equip = Game.GetEquipById("stringedYoyoNoPierce");
        //    Game.SetEquipAcquired(director, equip, false, true);
        //    GlobalState.AcquiringVirtualBadge = false;
        //    GlobalState.IsPhysicalBadgeAcquired = false;
        //    // TODO: Remove flag?

        //    LoggerInstance.Msg("Finished removing badge!");
        //}
    }
}
