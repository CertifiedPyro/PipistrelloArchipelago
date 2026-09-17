using Archipelago.MultiClient.Net.Colors;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

/// <summary>
/// Handler for receiving items from Archipelago.
/// </summary>
[HarmonyPatch]
internal static class ItemHandler
{
    private static readonly Dictionary<string, string> ItemToFlag = new()
    {
        { "Offstring Throw", Game.FLAG_ABILITY_THROW },
        { "Walk-the-Dog", Game.FLAG_ABILITY_WALKTHEDOG },
        { "Wall-Dash", Game.FLAG_ABILITY_WALLDASH },
        { "UFO Throw", Game.FLAG_ABILITY_HELIX },
        { "Wall-Ride", Game.FLAG_ABILITY_WALLRAILING },
        { "Sleeper", Game.FLAG_ABILITY_CHARGED_SLEEPER },
        { "Flurry Attack", Game.FLAG_ABILITY_CHARGED_FLURRY },
        { "Cat's Cradle", Game.FLAG_ABILITY_CHARGED_SPREAD },
        { "Merry-Go-Round", Game.FLAG_ABILITY_CHARGED_SPIN },
        { "Parry", Game.FLAG_ABILITY_SPECIAL_PARRY },
        { "Around-the-World", Game.FLAG_ABILITY_SPECIAL_SPIN },
        { "Coin-Flip", Game.FLAG_ABILITY_SPECIAL_COINFLIP },
        { "Mega-Battery 1", Game.FLAG_MEGABATTERY1 },
        { "Mega-Battery 2", Game.FLAG_MEGABATTERY2 },
        { "Faria Mega-Battery", Game.FLAG_MEGABATTERY2 },
        { "Mega-Battery 3", Game.FLAG_MEGABATTERY3 },
        { "Mega-Battery 4", Game.FLAG_MEGABATTERY4 },
    };

    private static readonly Dictionary<string, Game.Upgrade> ItemToUpgrade = Game.upgrades
        .ToArray()
        .ToDictionary(u => Localization.Get($"upgrade_name_{u.id}", "en_US"));

    private static readonly Dictionary<string, Game.Equip> ItemToEquip = Game.equips
        .ToArray()
        .ToDictionary(e => Localization.Get($"equip_name_{e.id}", "en_US"));

    private static bool _disableHandler;

    /// <summary>
    /// If loading save, reset internal state.
    /// </summary>
    [HarmonyPostfix, HarmonyPatch(typeof(Director), nameof(Director.InitFromSavefile))]
    private static void Director_InitFromSavefile_Postfix()
    {
        _disableHandler = false;
    }

    /// <summary>
    /// Process received items list.
    /// </summary>
    [HarmonyPrefix, HarmonyPatch(typeof(ObjectPlayer), nameof(ObjectPlayer.Process))]
    private static void ObjectPlayer_Process_Prefix()
    {
        if (!Global.State.SaveFileLoaded || _disableHandler)
        {
            return;
        }

        var director = Global.Director;
        try
        {
            var helper = Global.State.Session.Items;
            var lastIndex = director.GetFlag(Constants.FlagLastItemIndex);
            if (lastIndex > helper.AllItemsReceived.Count)
            {
                Global.State.Messages.Enqueue("[c:red|Unexpected item index. Please quit and reconnect.]");
                Melon<PipArchMod>.Logger.Error("Received item index was not expected.");
                Melon<PipArchMod>.Logger.Error($"Received index: {helper.Index} | Last index: {lastIndex}");
                _disableHandler = true;
                return;
            }

            while (lastIndex < helper.AllItemsReceived.Count)
            {
                var oldFlags = new Il2CppSystem.Collections.Generic.Dictionary<string, int>();
                Game.CopyFlags(director.playerRecord.flags, oldFlags);

                var item = helper.AllItemsReceived[lastIndex];
                var isItemFromLocalLocation = Utils.IsLocalItem(item)
                                              && Global.State.LocalCheckedLocations.ContainsKey(item.LocationId);
                var setting = ModSettings.MessagesItemReceivedAllowed.Value;
                var itemMessageAllowedFromSetting =
                    (item.Flags.HasFlag(ItemFlags.Advancement) && setting.HasFlag(ItemMessagesSetting.Progression))
                    || (item.Flags.HasFlag(ItemFlags.NeverExclude) && setting.HasFlag(ItemMessagesSetting.Useful))
                    || (item.Flags.HasFlag(ItemFlags.Trap) && setting.HasFlag(ItemMessagesSetting.Trap))
                    || (item.Flags == ItemFlags.None && setting.HasFlag(ItemMessagesSetting.Filler))
                    // Items granted by server have no flags, so always show if any messages are allowed.
                    || (item.Player?.Slot == 0 && setting != ItemMessagesSetting.None);
                var itemMessageAllowed = !isItemFromLocalLocation && itemMessageAllowedFromSetting;

                var result = HandleItem(item, itemMessageAllowed);
                if (!result)
                {
                    _disableHandler = true;
                    return;
                }

                director.SetFlag(Constants.FlagLastItemIndex, ++lastIndex);

                var newFlags = new Il2CppSystem.Collections.Generic.Dictionary<string, int>();
                Game.CopyFlags(director.playerRecord.flags, newFlags);

                // Determine which flags were modified during item handling.
                var modifiedFlags = new Dictionary<string, int>();
                foreach (var kvp in newFlags)
                {
                    if (!oldFlags.TryGetValue(kvp.Key, out var oldValue) || oldValue != kvp.Value)
                    {
                        modifiedFlags.Add(kvp.Key, kvp.Value);
                    }
                }

                // Put the modified flags directly into playerCheckpoint.
                // We don't call director.PrepareCheckpoint() since it can store temporary flags.
                director.playerCheckpoint.money = director.playerRecord.money;
                director.playerCheckpoint.petalContainers = director.playerRecord.petalContainers;
                director.playerCheckpoint.bpContainers = director.playerRecord.bpContainers;
                director.playerCheckpoint.followingObjectIds = director.playerRecord.followingObjectIds;
                foreach (var kvp in modifiedFlags)
                {
                    director.playerCheckpoint.flags[kvp.Key] = kvp.Value;
                }
            }

            // Dequeue all items.
            while (helper.DequeueItem() != null)
            {
            }
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {e}");
        }
    }

    /// <summary>
    /// Handles a received Archipelago item.
    /// </summary>
    /// <param name="item">The received item.</param>
    /// <param name="queueMessage">Whether to queue a message.</param>
    /// <returns>true if the item was handled successfully, false otherwise.</returns>
    private static bool HandleItem(ItemInfo item, bool queueMessage = false)
    {
        try
        {
            var itemName = item.ItemName;
            var director = Global.Director;
            var followingObjectIds = director.playerRecord.followingObjectIds.ToArray();
            var result = true;
            Melon<PipArchMod>.Logger.Msg($"Received item: {itemName}");

            if (ItemToFlag.TryGetValue(itemName, out var itemFlag))
            {
                director.SetFlagBool(itemFlag, true);
                Melon<PipArchMod>.Logger.Msg($"Set flag: {ItemToFlag[itemName]}");
            }
            else if (ItemToUpgrade.TryGetValue(itemName, out var upgrade))
            {
                Game.SetUpgradeAcquired(director, upgrade, true);
                Melon<PipArchMod>.Logger.Msg("Added upgrade");
            }
            else if (ItemToEquip.FirstOrDefault(pair => itemName.Contains(pair.Key)) is (not null, var equip))
            {
                if (Game.IsEquipAcquired(director.playerRecord, equip))
                {
                    Game.SetEquipRefined(director, equip, true);
                    Melon<PipArchMod>.Logger.Msg("Refined equip");
                }
                else
                {
                    Game.SetEquipAcquired(director, equip, true, true);
                    Melon<PipArchMod>.Logger.Msg("Added equip");
                }
            }
            else if (itemName.Contains('$')
                     && int.TryParse(itemName[(itemName.IndexOf('$') + 1)..], out var money))
            {
                // CollectCoin properly handles debts.
                director.CollectCoin(money);
                Melon<PipArchMod>.Logger.Msg("Added $" + money);
            }
            else if (itemName == "Petal Container")
            {
                Game.SetPetalContainerAcquired(director, "filler_pc", 1, true);
                Game.SetPetalContainerAcquired(director, "filler_pc", 0, false);
                Melon<PipArchMod>.Logger.Msg("Added petal container");
            }
            else if (itemName == "BP Shard")
            {
                Game.SetBpContainerAcquired(director, "filler_bp", 1, true);
                Game.SetBpContainerAcquired(director, "filler_bp", 0, false);
                Melon<PipArchMod>.Logger.Msg("Added BP shard");
            }
            else if (itemName == "Staff ID")
            {
                // Use Staff ID from a dev map that we know won't be changed.
                var staffIdObject = Utils.GetMapvaniaObject("yugo3_dev/yug4006/yug4042")!;
                // Check that the staff ID isn't already turned in for dungeon access or following the player.
                if (!director.GetFlagBool($"{Game.GLOBAL_FLAG_PREFIX}fariaLimeDungeonAccess")
                    && followingObjectIds.All(o => o.AsString != staffIdObject.globalObjectId.AsString))
                {
                    // TODO: Find better way to add following object that activates immediately.
                    director.playerRecord.followingObjectIds.Add(staffIdObject.globalObjectId);
                    Melon<PipArchMod>.Logger.Msg("Added Staff ID to following objects");
                }
            }
            else
            {
                result = false;
                Melon<PipArchMod>.Logger.Error($"Could not handle item: {itemName}");
            }

            if (result && queueMessage)
            {
                var itemDisplayName = item.ItemDisplayName.Replace(" ", "[nbsp]");
                var itemColor = Utils.GetTextColor(ColorUtils.GetColor(item).ToString());
                var playerName = item.Player.Name.Replace(" ", "[nbsp]");
                var playerColor = Utils.IsLocalItem(item)
                    ? Utils.GetTextColor(ColorUtils.ActivePlayerColor.ToString())
                    : Utils.GetTextColor(ColorUtils.NonActivePlayerColor.ToString());

                var text = Utils.IsLocalItem(item)
                    ? $"You found your [c:{itemColor}|{itemName}]!"
                    : $"You received [c:{itemColor}|{itemDisplayName}] from [c:{playerColor}|{playerName}]!";
                Global.State.Messages.Enqueue(text);
            }
            else if (!result)
            {
                var text = $"[c:red|Unexpected item: {item.ItemDisplayName}. Please quit and reconnect.]";
                Global.State.Messages.Enqueue(text);
            }

            return result;
        }
        catch (Exception e)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling item: {e}");
            return false;
        }
    }
}
