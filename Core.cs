using HarmonyLib;
using Il2CppPipistrello;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using Il2CppUtil;
using Archipelago.MultiClient.Net;

[assembly: MelonInfo(typeof(PipistrelloArchipelago.Core), "PipistrelloArchipelago", "0.1.0", "CertifiedPyro", null)]
[assembly: MelonGame("Pocket Trap", "Pipistrello")]

namespace PipistrelloArchipelago;

public static class GlobalState
{
    public static Director Director = null;

    public static bool AcquiringPhysicalArchItem = false;
    public static Queue<string> AcquiringVirtualArchItemStrings = new();

    public static Dictionary<string, Game.GlobalObjectId> SwappedItems = new()
    {
        { new Game.GlobalObjectId { mapId = "city", roomId = "ren223", objectId = "yug5534" }.AsString, new Game.GlobalObjectId() },  // Petal container (South plaza)
        { new Game.GlobalObjectId { mapId = "city", roomId = "lor2248", objectId = "yug4337" }.AsString, new Game.GlobalObjectId() },  // Golden Badge (walletAttackUp) (South Plaza)
        { new Game.GlobalObjectId { mapId = "city", roomId = "yug5210", objectId = "yug5250" }.AsString, new Game.GlobalObjectId() },  // Pitcher's Badge blueprint (thrownObjectAttackUp) (South Plaza)
        { new Game.GlobalObjectId { mapId = "city_interiors", roomId = "ren1362", objectId = "ren1605" }.AsString, new Game.GlobalObjectId() }  // Equip (in house, top left map)
    };
}

[HarmonyPatch(typeof(Director), nameof(Director.Init))]
public class DirectorInitPatch
{
    public static void Postfix(Director __instance)
    {
        GlobalState.Director = __instance;

        //var session = ArchipelagoSessionFactory.CreateSession("archipelago.gg", 49317);
        //LoginResult result;
        //try
        //{
        //    result = session.TryConnectAndLogin("TUNIC", "PyroTunic", Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags.AllItems);
        //}
        //catch (Exception e)
        //{
        //    result = new LoginFailure(e.GetBaseException().Message);
        //}

        //if (result is LoginSuccessful loginSuccess)
        //{
        //    MelonLogger.Msg($"Connected successfully with Archipelago server!");
        //}
        //else
        //{
        //    MelonLogger.Msg($"Failed to connect with Archipelago server!");
        //    var loginFailure = (LoginFailure)result;
        //    foreach (var error in loginFailure.Errors)
        //    {
        //        MelonLogger.Msg(error);
        //    }
        //}
    }
}

[HarmonyPatch(typeof(Director), nameof(Director.InstantiateFromMap))]
public static class DirectorPatch
{
    public static void Prefix(ref Mapvania.Object mapObj)
    {
        // Check if item needs to be swapped.
        if (!GlobalState.SwappedItems.TryGetValue(mapObj.globalObjectId.AsString, out var swappedGlobalObjectId))
        {
            return;
        }

        // Swap to a physical Archipelago item.
        if (swappedGlobalObjectId.mapId == null && swappedGlobalObjectId.roomId == null && swappedGlobalObjectId.objectId == null)
        {
            MelonLogger.Msg($"Director.InstantiateFromMap() Prefix: {mapObj.globalObjectId.AsString} -> Arch item");
            mapObj.objectDefId = "lor313";
            mapObj.objectDefName = "bpContainer";

            var globalObjectId = mapObj.globalObjectId;
            globalObjectId.objectId += Constants.ArchItemObjectIdSuffix;
            mapObj.globalObjectId = globalObjectId;
        }
        // TODO: Swap items with each other.
    }

    public static void Postfix(Il2CppPipistrello.Object __result)
    {
        if (Utils.IsArchItemId(__result?.globalObjectId?.objectId))
        {
            __result.spriteName = Constants.ArchSpriteName;
        }
    }
}

[HarmonyPatch(typeof(Game))]
public static class GamePatch
{
    [HarmonyPatch(nameof(Game.SetBpContainerAcquired))]
    [HarmonyPrefix]
    public static bool PrefixSetBpContainerAcquired(string id, bool acquired, ref bool __result)
    {
        // If this is an physical Archipelago item pretending to be a BP container, don't actually pick up the BP container.
        if (Utils.IsArchItemId(id))
        {
            // Still flag the item as acquired, so it doesn't show up again.
            GlobalState.Director.SetFlagBool(Game.FlagBpContainerAcquired(id), acquired);
            GlobalState.AcquiringPhysicalArchItem = true;
            __result = false;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(Minimap))]
public static class MinimapPatch
{
    [HarmonyPatch(nameof(Minimap.RefreshPins))]
    public static void Prefix()
    {
        var mapPins = GlobalState.Director.playerRecord.mapPins;
        for (var i = 0; i < mapPins.Count; i++)
        {
            var mapPin = mapPins[i];

            // Replace map pins for physical Archipelago items with the Archipelago UI pin.
            if (Utils.IsArchItemId(mapPin.objectId.objectId))
            {
                mapPin.pinId = Constants.ArchMapPinSpriteName;
                mapPins.System_Collections_IList_set_Item(i, mapPin);
            }

            // Replace map pins for the original items with the Archipelago UI pin.
            if (GlobalState.SwappedItems.ContainsKey(mapPin.objectId.AsString))
            {
                mapPin.pinId = Constants.ArchMapPinSpriteName;
                mapPins.System_Collections_IList_set_Item(i, mapPin);
            }
        }
    }
}

[HarmonyPatch(typeof(InstructionPanel))]
public static class InstructionPanelPatch
{
    private const long TextShowTimeMs = 3000;
    private const long TextCooldownTimeMs = 250;

    private static long? TextShowStartMs = null;
    private static long? TextCooldownStartMs = null;

    [HarmonyPatch(nameof(InstructionPanel.Process))]
    public static void Prefix(InstructionPanel __instance)
    {
        // Check if InstructionPanel is off cooldown.
        if (TextCooldownStartMs.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - TextCooldownStartMs > TextCooldownTimeMs)
        {
            TextCooldownStartMs = null;
        }

        // Check if InstructionPanel should show due to acquired physical Archipelago item.
        // TODO: Check that InstructionPanel isn't already showing.
        if (!TextShowStartMs.HasValue && !TextCooldownStartMs.HasValue && GlobalState.AcquiringVirtualArchItemStrings.Count > 0)
        {
            var text = GlobalState.AcquiringVirtualArchItemStrings.Dequeue();
            __instance.SetInstruction(text, true);
            TextShowStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    [HarmonyPatch(nameof(InstructionPanel.GetInstructionText))]
    public static bool Prefix(string id, InstructionPanel __instance, ref string __result)
    {
        // If InstructionPanel should show due to acquired physical Archipelago item, return proper string.
        if (TextShowStartMs != null)
        {
            // Check if InstructionPanel should be on cooldown.
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - TextShowStartMs > TextShowTimeMs)
            {
                TextShowStartMs = null;
                TextCooldownStartMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                __instance.SetInstruction(null, true);
                return true;
            }

            __result = id;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(DialoguePanel))]
public class DialoguePanelPatch
{
    private static bool ShowedArchItemDialogue = false;

    [HarmonyPatch(nameof(DialoguePanel.InjectText))]
    public static bool Prefix(ref string text)
    {
        //MelonLogger.Msg($"DialoguePanel InjectText() original args: {text}");
        // Show player that they acquired a physical Archipelago item.
        if (GlobalState.AcquiringPhysicalArchItem)
        {
            if (!ShowedArchItemDialogue)
            {
                text = "[instant|You got a [c:blue|Archipelago item]!][w:2]";
                ShowedArchItemDialogue = true;
            }
            else
            {
                // Don't show the remaining original dialogue.
                return false;
            }
        }

        return true;
    }

    [HarmonyPatch(nameof(DialoguePanel.IsOver))]
    public static void Postfix(ref bool __result)
    {
        // Remove replaced text once dialogue is over.
        if (__result)
        {
            GlobalState.AcquiringPhysicalArchItem = false;
            ShowedArchItemDialogue = false;
        }
    }
}

[HarmonyPatch(typeof(ObjectWarpArea))]
public class ObjectWarpAreaPatch
{
    [HarmonyPatch(nameof(ObjectWarpArea.CalculateIsHousePuzzleCompleted))]
    public static bool Prefix(ObjectWarpArea __instance, ref bool __result)
    {
        if (!GlobalState.Director.currentProject.housePuzzleFlags.TryGetValue(__instance.globalObjectId.AsString, out var houseFlags))
        {
            return true;
        }

        foreach (var flag in houseFlags)
        {
            // TODO: Handle other types of objects
            var newFlag = flag;
            if (flag.StartsWith(Game.FLAG_EQUIP_PREFIX))
            {
                var startIndex = Game.FLAG_EQUIP_PREFIX.Length;
                var endIndex = flag.IndexOf(':', startIndex);
                var equipId = flag[startIndex..endIndex];

                // Find equip based on name
                Game.GlobalObjectId equipGlobalObjectId = null;
                foreach (var meta in GlobalState.Director.currentProject.equipMeta)
                {
                    if (meta.equipId == equipId)
                    {
                        equipGlobalObjectId = meta.globalObjectId;
                    }
                }

                var isSwapped = GlobalState.SwappedItems.TryGetValue(equipGlobalObjectId.AsString, out var swappedGlobalObjectId);
                if (isSwapped)
                {
                    // Swap to an archipelago item flag.
                    if (swappedGlobalObjectId.mapId == null && swappedGlobalObjectId.roomId == null && swappedGlobalObjectId.objectId == null)
                    {
                        newFlag = Game.FlagBpContainerAcquired(equipGlobalObjectId.AsString + Constants.ArchItemObjectIdSuffix);
                    }
                    // TODO: Swap items with each other
                }
            }

            if (!GlobalState.Director.GetFlagBool(newFlag))
            {
                __result = false;
                return false;
            }
        }

        __result = true;
        return false;
    }
}

public class Core : MelonMod
{
    private const KeyCode addKey = KeyCode.LeftControl;
    private const KeyCode removeKey = KeyCode.LeftAlt;

    private const KeyCode petalKey = KeyCode.U;
    private const KeyCode bpKey = KeyCode.I;
    private const KeyCode equipKey = KeyCode.O;
    private const KeyCode upgradeKey = KeyCode.P;

    private const KeyCode movementAbilityKey = KeyCode.J;
    private const KeyCode chargedActionKey = KeyCode.K;
    private const KeyCode specialActionKey = KeyCode.L;
    private const KeyCode megaBatteryKey = KeyCode.Semicolon;

    // mapId/roomId/objectId
    // objectDefId=lor104, objectDefName=petalContainer
    // position.x=792, position.y=456, width=16, height=16
    private static string petalContainerId = "city/ren223/yug5534";
    private static string bpContainerId = "city/yug5154/yug5202"; // mapId/roomId/objectId, objectDefId=lor313, objectDefName=bpContainer

    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Initialized.");
        ExportArchipelagoSprites();

        //var res = new List<Mapvania.Object>();
        //var proj = new Mapvania.Project();
        //Mapvania.ReloadMaps(proj);
        //foreach (Mapvania.Map map in proj.maps)
        //{
        //    foreach (Mapvania.Room room in map.rooms)
        //    {
        //        foreach (Mapvania.Object obj in room.objects)
        //        {
        //            if (obj.objectDefName == "petalContainer" && obj.globalObjectId.objectId == "yug5534")
        //            {
        //                MelonLogger.Msg($"{obj.objectDefId},{obj.objectDefName},{obj.objectDefBehaviorName}");
        //                MelonLogger.Msg($"{obj.globalObjectId.mapId},{obj.globalObjectId.roomId},{obj.globalObjectId.objectId}");
        //                MelonLogger.Msg($"{obj.tag}");
        //                MelonLogger.Msg($"{obj.position.x},{obj.position.y},{obj.width},{obj.height}");
        //                MelonLogger.Msg($"{obj.properties.objectFields.Count}, {obj.properties.Serialize()}");
        //                MelonLogger.Msg($"{obj.usesFlags} {obj.usesPresenceFlag} {obj.isDev}");
        //            }
        //            //res.Add(obj);
        //        }
        //    }
        //}
    }

    public override void OnLateUpdate()
    {
        var director = GlobalState.Director;

        if (Input.GetKeyDown(KeyCode.Slash))
        {
            RoomConnections.Postfix(director);
        }

        // Petal containers (health)
        if (Input.GetKeyDown(petalKey) && Input.GetKey(addKey))
        {
            LoggerInstance.Msg("Adding virtual petal container!");
            var result = Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, true);
            if (result)
            {
                var text = $"You got a [c:rose|Petal Container]!\n[c:rose|Petals] collected: [c:blue|{GlobalState.Director.playerRecord.petalContainers}] / {Game.PETAL_COLLECTIBLES}";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual petal container!");
        }
        else if (Input.GetKeyDown(petalKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing petal container!");
            Game.SetPetalContainerAcquired(director, "city/ren223/yug5534", 1, false);
            LoggerInstance.Msg("Finished removing petal container!");
        }
        // BP shards
        else if (Input.GetKeyDown(bpKey) && Input.GetKey(addKey))
        {
            LoggerInstance.Msg("Adding virtual BP container!");
            var result = Game.SetBpContainerAcquired(director, "city/yug5154/yug5202", 1, true);
            if (result)
            {
                var text = $"You got a [c:bp|BP Shard]!\n[c:bp|BP Shards] collected: [c:blue|{GlobalState.Director.playerRecord.bpContainers}] / {Game.BPSHARD_COLLECTIBLES}";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual BP container!");
        }
        else if (Input.GetKeyDown(bpKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing BP container!");
            Game.SetBpContainerAcquired(director, "city/yug5154/yug5202", 1, false);
            LoggerInstance.Msg("Finished removing BP container!");
        }
        // Badges (equips)
        else if (Input.GetKeyDown(equipKey) && Input.GetKey(addKey))
        {
            // TODO: Factor in badge upgrade for name
            LoggerInstance.Msg("Adding virtual badge!");
            var equip = Game.GetEquipById("thrownObjectAttackUp");
            var result = Game.SetEquipAcquired(director, equip, true, true);
            if (result)
            {
                // TODO: Factor in removing cheater's badges
                var text = $"You got the [c:equip|{Game.GetEquipName(equip, false)}]!\n[c:equip|Equips] collected: {GlobalState.Director.playerStatus.equipsAcquired} / {Game.EQUIP_COLLECTIBLES}";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual badge!");
        }
        else if (Input.GetKeyDown(equipKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing badge!");
            Game.SetEquipAcquired(director, Game.GetEquipById("stringedYoyoNoPierce"), false, true);
            LoggerInstance.Msg("Finished removing badge!");
        }

        // Upgrades
        else if (Input.GetKeyDown(upgradeKey) && Input.GetKey(addKey))
        {
            // TODO: Disable upgrade shop
            LoggerInstance.Msg("Adding virtual upgrade!");
            var upgrade = Game.GetUpgradeById("bpUp");
            var result = Game.SetUpgradeAcquired(director, upgrade, true);
            if (result)
            {
                var text = $"You got the [c:upgrade|{Game.GetUpgradeName(upgrade)}] upgrade!\n[c:upgrade|Upgrades] collected: [c:blue|{GlobalState.Director.playerStatus.upgradesAcquired}] / {Game.upgrades.Count}";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual upgrade!");
        }
        else if (Input.GetKeyDown(upgradeKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing upgrade!");
            Game.SetUpgradeAcquired(director, Game.GetUpgradeById("bpUp"), false);
            LoggerInstance.Msg("Finished removing upgrade!");
        }
        // Movement abilities
        else if (Input.GetKeyDown(movementAbilityKey) && Input.GetKey(addKey))
        {
            LoggerInstance.Msg("Adding virtual movement ability!");
            var flag = GlobalState.Director.GetFlagBool(Game.FLAG_ABILITY_WALKTHEDOG);
            if (!flag)
            {
                // TODO: Figure out how to convert ability flag to ability name
                GlobalState.Director.SetFlagBool(Game.FLAG_ABILITY_WALKTHEDOG, true);
                var text = $"You've learned the [c|{Localization.Get("ui_ability_walkTheDog")}] ability!";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual movement ability!");
        }
        else if (Input.GetKeyDown(movementAbilityKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing movement ability!");
            GlobalState.Director.SetFlagBool(Game.FLAG_ABILITY_WALKTHEDOG, false);
            LoggerInstance.Msg("Finished removing movement ability!");
        }
        // Charged moves
        else if (Input.GetKeyDown(chargedActionKey) && Input.GetKey(addKey))
        {
            LoggerInstance.Msg("Adding virtual charged move!");
            var flag = GlobalState.Director.GetFlagBool(Game.FLAG_ABILITY_CHARGED_SLEEPER);
            if (!flag)
            {
                // TODO: Figure out how to convert ability flag to ability name
                // TODO: Should this replace current equipped move?
                GlobalState.Director.SetFlagBool(Game.FLAG_ABILITY_CHARGED_SLEEPER, true);
                var text = $"You've learned the [c|{Localization.Get("ui_ability_sleeper_name")}] Charged Move!";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual charged move!");
        }
        else if (Input.GetKeyDown(chargedActionKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing charged move!");
            GlobalState.Director.SetFlagBool(Game.FLAG_ABILITY_CHARGED_SLEEPER, false);
            GlobalState.Director.SetFlag(Game.FLAG_EQUIPPED_CHARGED_ACTION, 0);
            LoggerInstance.Msg("Finished removing charged move!");
        }
        // Special moves
        else if (Input.GetKeyDown(specialActionKey) && Input.GetKey(addKey))
        {
            LoggerInstance.Msg("Adding virtual special move!");
            var flag = GlobalState.Director.GetFlagBool(Game.FLAG_ABILITY_SPECIAL_PARRY);
            if (!flag)
            {
                // TODO: Figure out how to convert ability flag to ability name
                // TODO: Should this replace current equipped move?
                GlobalState.Director.SetFlagBool(Game.FLAG_ABILITY_SPECIAL_PARRY, true);
                var text = $"You've learned the [c|{Localization.Get("ui_ability_parry_name")}] Special Move!";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual special move!");
        }
        else if (Input.GetKeyDown(specialActionKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing special move!");
            GlobalState.Director.SetFlagBool(Game.FLAG_ABILITY_SPECIAL_PARRY, false);
            GlobalState.Director.SetFlag(Game.FLAG_EQUIPPED_SPECIAL_ACTION, 0);
            LoggerInstance.Msg("Finished removing special move!");
        }
        // Mega-Batteries
        else if (Input.GetKeyDown(megaBatteryKey) && Input.GetKey(addKey))
        {
            LoggerInstance.Msg("Adding virtual mega battery!");
            var flag = GlobalState.Director.GetFlagBool(Game.FLAG_MEGABATTERY2);
            if (!flag)
            {
                GlobalState.Director.SetFlagBool(Game.FLAG_MEGABATTERY2, true);
                var text = $"You got a [c|Mega-Battery]!";
                GlobalState.AcquiringVirtualArchItemStrings.Enqueue(text);
            }
            LoggerInstance.Msg("Finished adding virtual mega battery!");
        }
        else if (Input.GetKeyDown(megaBatteryKey) && Input.GetKey(removeKey))
        {
            LoggerInstance.Msg("Removing mega battery!");
            GlobalState.Director.SetFlagBool(Game.FLAG_MEGABATTERY2, false);
            LoggerInstance.Msg("Finished removing mega battery!");
        }
    }

    private void ExportArchipelagoSprites()
    {
        try
        {
            var filesToPaths = new Dictionary<string, string>()
            {
                { "arch_medium.png", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites") },
                { "arch_small.png", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Sprites", "ui", "mapPins") }
            };
            foreach (var (file, path) in filesToPaths)
            {
                var fullPath = Path.Combine(path, file);

                // Write the file if it's missing or outdated
                // Tip: You can remove the File.Exists check during dev to ensure it overwrites with your latest icon
                var data = LoadBytesFromResource($"PipistrelloArchipelago.{file}");
                if (data != null)
                {
                    File.WriteAllBytes(fullPath, data);
                    MelonLogger.Msg($"Archipelago sprite deployed to: {fullPath}");
                }
                else
                {
                    MelonLogger.Error($"Could not find embedded resource 'PipistrelloArchipelago.{file}'");
                }
            }
        }
        catch (Exception e)
        {
            MelonLogger.Error($"Failed to export sprite: {e.Message}");
        }
    }

    private static byte[] LoadBytesFromResource(string path)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using Stream stream = assembly.GetManifestResourceStream(path);
        if (stream == null) return null;
        var buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        return buffer;
    }
}
