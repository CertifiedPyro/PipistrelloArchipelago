# Pipistrello and the Cursed Yoyo Archipelago Mod

## Required Software

* [Archipelago](https://github.com/ArchipelagoMW/Archipelago/releases)
* [MelonLoader](https://melonwiki.xyz/#/README?id=requirements)
  * Requires [Microsoft Visual C++ 2015-2019 Redistributable 64 Bit](https://aka.ms/vs/16/release/vc_redist.x64.exe)
  * Requires [dotnet 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0#runtime-desktop-6.0.19)
* [MelonPreferencesManager](https://github.com/piepieonline/MelonPreferencesManager/releases)
* [Pipistrello Archipelago Mod](https://github.com/CertifiedPyro/PipistrelloArchipelago/releases)

## Installation

### Prerequisites
1. Run the MelonLoader installer, select "Pipistrello and the Cursed Yoyo", and click "Install".
2. If you're running Linux:
   1. Export the following environment variable: `WINEDLLOVERRIDES="version=n,b"`
   2. On Steam, you can set the launch options to: `WINEDLLOVERRIDES="version=n,b" %command%`
3. Launch the game to create the required mod folders.
4. Navigate to your game's installation installation folder.
    1. You can find this in Steam by right-clicking > Manage > Browse Local Files.
    2. This is usually  `C:\Program Files (x86)\Steam\steamapps\common\Pipistrello and the Cursed Yoyo`
5. Download the latest release of MelonPreferencesManager.
6. Extract the two dlls into the `Mods/` folder under your game's installation directory.

### Archipelago mod

1. Download `PipistrelloArchipelago.zip` from the latest release.
2. Extract the .zip file's contents directly into the game's installation folder.
    1. Make sure `PipistrelloArchipelago.dll` is in `Mods/` and `Archipelago.MultiClient.Net.dll` is in `UserLibs/`.
3. Launch the game, and you should see a Connect button on the main menu.

### Archipelago tools

1. Make sure Archipelago is installed.
2. Download `pipistrello.apworld` from the latest release.
3. Double-click on `pipistrello.apworld`. Archipelago should install the apworld automatically.
4. Open the Archipelago Launcher and run "Generate Template Options" to create the options template file.
    1. Alternatively, you can download `Pipistrello.and.the.Cursed.Yoyo.yaml` from the latest release.

## Generating a game

Follow [the official instructions](https://archipelago.gg/tutorial/Archipelago/setup_en#generating-a-game).

## Joining a multiworld game
1. Start the game after installing all necessary mods.
2. Press F5 to open MelonPreferencesManager and input your connection information.
3. Press the **Connect** button in-game.
4. Once connected, you can press **Load Game**.
5. Start with a *new* save file. The game will stall for a few seconds before loading.
6. You should now be starting in South Plaza!

## Hints and trackers

If you need to interact with the server, you can use the Archipelago Text Client. There is no visual tracker yet, so
please use Universal Tracker for now.

## What does randomization do to this game?

Currently, the randomizer is in early alpha, so only the South Plaza is randomized.
Most items and rewards are randomized and replaced with location checks.

The upgrade tree and badge refinements *are not* currently randomized.

## What items get shuffled?

By default, the following items are shuffled:
- Abilities
- Badges
  - Badges are progressive. The first item will be the base badge, and the second item will be the refined badge.
- BP shards
- Charged moves
- Petal containers
- Special moves
- Upgrades

Money bags are given as filler items.

Due to the small location pool in South Plaza, there are no upgrades in the item pool.
There may also be no charged moves if only the basic checks are enabled.

## What locations get shuffled?

By default, the following locations are enabled:
- Badges
- BP Shards
- Combats (required)
- Musical Notes
- Petal containers
- Quests (burger, etc.)
- Taxi phones unlock

Additionally, the following locations can be optionally included:
- Combats (optional)
- Money bags (standalone)
  - Money bags from combats/quests/etc are always enabled as locations

The following locations are **not** enabled:
- Diamonds
  - Excluded because turn-in is in 2nd half of the game

## What does another world's item look like in Pipistrello and the Cursed Yoyo?

Items from other worlds show up as an Archipelago sprite in the game world.
An Archipelago sprite is also shown on the map.
When you collect another world's item, you'll get a message showing the item name and recipient.

## When the player receives an item, what happens?

The item is instantly granted to you, and a message appears on the bottom of the screen showing the item name and sender.

## Credits
- Scipio for the Archipelago sprites used in-game
