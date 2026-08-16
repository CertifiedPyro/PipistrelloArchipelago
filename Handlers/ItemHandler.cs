using Archipelago.MultiClient.Net.Models;
using Il2CppPipistrello;
using Il2CppUtil;
using MelonLoader;

namespace PipistrelloArchipelago.Handlers;

public static class ItemHandler
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
        { "Mega-Battery 3", Game.FLAG_MEGABATTERY3 },
        { "Mega-Battery 4", Game.FLAG_MEGABATTERY4 }
    };

    private static readonly Dictionary<string, Game.Upgrade> ItemToUpgrade = Game.upgrades
        .ToArray()
        .ToDictionary(u => Localization.Get($"upgrade_name_{u.id}", "en_US"));

    private static readonly Dictionary<string, Game.Equip> ItemToEquip = Game.equips
        .ToArray()
        .ToDictionary(e => Localization.Get($"equip_name_{e.id}", "en_US"));

    private static bool _isItemHandlerEnabled;

    public static bool IsStarted()
    {
        return _isItemHandlerEnabled;
    }

    /// <summary>
    /// Starts the received item event loop.
    /// </summary>
    public static async Task Start()
    {
        _isItemHandlerEnabled = true;
        var director = Global.Director;
        try
        {
            while (true)
            {
                await Task.Delay(1000);

                if (!Global.State.SaveFileLoaded)
                {
                    continue;
                }

                var helper = Global.State.Session.Items;
                var lastIndex = director.GetFlag(Constants.FlagLastItemIndex);
                if (lastIndex > helper.AllItemsReceived.Count)
                {
                    Global.State.Messages.Enqueue("[instant|[c:red|Unexpected item index. Please reconnect.]][w:2]");
                    Melon<PipArchMod>.Logger.Error("Received item index was not expected.");
                    Melon<PipArchMod>.Logger.Error($"Received index: {helper.Index} | Last index: {lastIndex}");
                    return;
                }

                while (lastIndex < helper.AllItemsReceived.Count)
                {
                    var item = helper.AllItemsReceived[lastIndex];
                    var isLocalLocation = item.Player.Slot == Global.State.Session.ConnectionInfo.Slot &&
                                          Global.State.LocalCheckedLocations.ContainsKey(item.LocationId);
                    var result = HandleItem(item, !isLocalLocation);
                    if (!result)
                    {
                        return;
                    }

                    director.SetFlag(Constants.FlagLastItemIndex, ++lastIndex);
                    director.PrepareCheckpoint(false);
                }

                // Dequeue all items.
                while (helper.DequeueItem() != null)
                {
                }
            }
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error($"Exception receiving item: {ex}");
        }
        finally
        {
            _isItemHandlerEnabled = false;
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
            var followingObjects = director.player.followingObjects.ToArray();
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
                Global.Director.CollectCoin(money);
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
                    && followingObjects.All(o => o.objectDefName != staffIdObject.objectDefName))
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
                var playerName = item.Player.Name.Replace(" ", "[nbsp]");

                var text = $"[instant|You received [c:blue|{itemDisplayName}] from [c:red|{playerName}]!][w:2]";
                Global.State.Messages.Enqueue(text);
            }
            else if (!result)
            {
                var text = $"[instant|[c:red|Unexpected item: {item.ItemDisplayName}. Please reconnect]][w:2]";
                Global.State.Messages.Enqueue(text);
            }

            return result;
        }
        catch (Exception ex)
        {
            Melon<PipArchMod>.Logger.Error($"Exception handling item: {ex}");
            return false;
        }
    }
}