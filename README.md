# Rogues V Raiders

Roaming Rogue and Raider warbands for SPT. They patrol, take points, travel between them, and fight you and
each other. Squads sit on top of the normal bot population and never eat scav or PMC spawn slots.

## Behaviour

Squads are a leader plus two to four others — three to five bodies. Each one rolls separately for whether it
starts on the map or arrives later, at about a 40/60 split; the latecomers turn up somewhere between the 10%
and 60% mark of the raid clock, scaled to whatever that map's timer is. So the place keeps changing after the
first ten minutes. They pick objectives, hold points of interest, move between them, and hunt.

Rogues and Raiders are hostile to each other, and to the custom factions if you run them. Player treatment
follows the Friendliness setting, which defaults to faction-authentic: Rogues keep their Lighthouse manners
and warn USEC before shooting, BEAR gets no warning, Raiders shoot everyone.

Where they roam:

| Faction | Maps |
|---|---|
| Rogues | Customs, Woods, Shoreline, Lighthouse |
| Raiders | Reserve, both Factories, Interchange, Labs |
| Both | Streets, both Ground Zero tiers |

Overlap maps roll for both factions independently, so that's where the two actually run into each other.

## Kit

Tuned to hurt, by default. Difficulty sits at impossible, weapons come in at 92–100% durability, armour
spawns within 5 durability points of full, ammo is weighted 60/25/10 across the top three penetrating
rounds per calibre with nothing below that ever rolled, and gear is gated to armour class 4 and up.

Armour is stated in points rather than a percentage on purpose. SPT only applies a percentage band to armour
on bots it considers PMCs, and `exUsec`/`pmcBot` aren't in that set — so their armour starts at the item's
full durability and comes off by a flat roll of 0–5 points (`armorMaxDelta`). What that works out to as a
percentage depends on the piece: 5 points off a 15-durability helmet is a lot, off an 85-durability shield
it's nothing. `armorMaxDelta` is the only armour durability knob the server actually reads for these roles.

These upgrades are faction-wide, which is worth understanding before you tune them. They rewrite the shared
exUsec and pmcBot data, so the ordinary Rogues and Raiders already on the map get them too, not only the
squads this adds. Each one has its own switch — `upgradeDurability`, `upgradeAmmo`, `upgradeGearTier` — so
turn off whichever you don't want. `forceHardestDifficulty` is a separate thing and does less than the name
suggests: it only pushes the vanilla exUsec and pmcBot waves onto the same difficulty tier. Switching it off
leaves the gear and ammo upgrades reaching vanilla spawns regardless.

## Spawn chance

10% at level 15, +5% every 5 levels, capped at 25%. Solo uses your level; in Fika it averages the squad.

The catch worth knowing up front: **Scale Chance By Level is on by default, and while it's on it overrides
`rogueChance`, `raiderChance` and `overlapChance` completely.** Set a chance in the config, see nothing
change, that's why. Turn the toggle off and the config numbers take over.

## Install

Extract into your SPT folder — the one holding `EscapeFromTarkov.exe` and `BepInEx`. Merge `SPT` and
`BepInEx` when Windows asks, then restart the server. That puts:

- `RoguesVRaidersServer` → `SPT/user/mods/`
- `RoguesVRaiders` → `BepInEx/plugins/`

Upgrading from 0.1.1 or earlier: those builds put the server mod in `user\mods\RoguesVRaidersServer` at the
install root by mistake, where the server never read it. Delete that stray `user` folder — the real one is
`SPT\user\mods\RoguesVRaidersServer`.

On Fika, the server mod goes with the SPT server and the client plugin goes on whichever machine owns the
raid, headless included. Everyone else needs nothing.

## Requires

**DrakiaXYZ-BigBrain** is the only hard dependency. **SAIN** is read by reflection rather than referenced —
without it squads still spawn and roam, they just fight on the vanilla brain. If SAIN is installed but a
version change makes it unreadable, the squads hand movement back to it rather than fight it for the mover,
and say so once in the log. **DrakiaXYZ-Waypoints** is optional but its expanded navmesh noticeably improves
patrol coverage. **Fika** only matters for co-op.

## Compatibility

- **Acid's Bot Placement System** rebuilds `BossLocationSpawn` from scratch at boot and after every raid,
  which wipes anything anyone else injected. Handled: the injector re-runs on raid start, after ABPS's pass
  and before the raid snapshot is taken. Nothing for you to configure.
- **ORBIT** must keep `Vanilla raiders (RESTART)` set to `true`. With it true ORBIT leaves exUsec and pmcBot
  alone. Flip it false and ORBIT starts driving the same bots this does, and the two will fight over the
  mover.
- **Other gear or difficulty mods that touch exUsec or pmcBot** lose to this one. The upgrade block rewrites
  durability, ammo pools, equipment pools and armour plate weighting in place, and this mod deliberately
  loads late so it lands after ABPS — which means it also lands after most others. Nothing is written to
  disk, so it's all undone by a server restart, but for the session the last writer wins. Turn off the
  individual `upgrade*` switches for whichever part you'd rather the other mod owned.

## Config

`SPT/user/mods/RoguesVRaidersServer/config.jsonc`, server restart to apply: faction map lists, per-map chance
overrides (Reserve and Labs are dialled back to 15), `escortAmount`, spawn timing (`startSpawnShare`,
`midRaidEarliest`, `midRaidLatest`), and the upgrade block (`difficulty`, `forceHardestDifficulty`,
`upgradeDurability`, `upgradeAmmo`, `ammoRankWeights`, `upgradeGearTier`, `minArmorClass`).

Values are checked as they load. Anything unusable — an empty `escortAmount`, a chance of 5000, a `null`
where a list belongs — clamps into range or reverts to its default and says which in the server log. A
config the mod can't use at all leaves the mod inert for that session instead of stopping the server.

F12, section *Rogues V Raiders*: Enable, Rogue Squads, Raider Squads, Friendliness, Roaming Objectives,
Objective Layer Priority, Lockdown POIs, Lockdown Delay, Broad AI Hostility, Hunt and Takeover, Min Spawn
Distance, Alive Bot Ceiling, Force Spawn Over Bot Cap, Scale Chance By Level, Verbose Logs.

Squad sizes assume the As Online bot amount setting.

## Build

`dotnet build -c Release` with the .NET SDK 9+. `Client` and `Core` are net472, `Server` is net9.0. `Core`
holds the logic that isn't tied to the game assemblies and is unit-tested — `dotnet test` runs `Client.Tests`
and `Server.Tests`. Two anchors resolve the install: `SptRoot` (the game folder) in Client, `SptServer` (the
`SPT` folder inside it) in Server.

Built against SPT 4.0.13 / EFT 0.16.9.
