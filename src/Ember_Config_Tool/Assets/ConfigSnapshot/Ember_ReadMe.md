# Configuration overview

Ember loads configuration in this order:

1. `/src/Config/*.lua` (module configs)
2. `/src/User_Config_Overrides.lua` (optional, **loaded last**)

Because the user override file loads last, any variables you set there will take precedence over everything in `/src/Config`.

## Configuration Files
All mod config files can be found in one of two locations:

- `/src/Config/` (primary configs)
- `/src/User_Config_Overrides.lua` (update-safe user overrides)

| Feature                   | Config File                                  |
| :------------------------ | :------------------------------------------- |
| General tweaks            | `src/Config/Ember_Config.lua`                |
| Debug logging             | `src/Config/Debug_Level.lua`                 |
| Glider tuning             | `src/Config/Glider_Stats_Config.lua`         |
| Terrain replacement       | `src/Config/Terrain_Replacer_Config.lua`     |
| Block replacement         | `src/Config/Block_Replacer_Config.lua`       |
| Terraforming properties   | `src/Config/Terraforming_Tweaks_Config.lua`  |
| Blueprint injector        | `src/Config/Custom_Blueprint_Config.lua`     |
| Expanded in-game settings | `src/Config/ExpandedGameSettings_Config.lua` |
| Fog tuning                | `src/Config/Fog_Tweaks_Config.lua`           |
| Map tweaks                | `src/Config/Map_Tweaks_Config.lua`           |
| Skill tweaks: Updraft     | `src/Config/SkillTweaks_Updraft_Config.lua`  |
| Gem tweaks                | `src/Config/Gem_Tweaks_Config.lua`           |
| Crafting tweaks           | `src/Config/Crafting_Tweaks_Config.lua`      |
| Fishing tweaks            | `src/Config/Fishing_Tweaks_Config.lua`       |
| Loot tweaks               | `src/Config/Loot_Tweaks_Config.lua`          |

## How to edit configs

1. Open the relevant `src/Config/*.lua` file.
2. Set the master toggle for that feature to `true`.
3. Adjust values.
4. Save the file.

### Notes on `nil` usage
Some configs intentionally use `nil` for certain fields. Eventually most of them will.
`nil` means “do not patch/change this value” / leave vanilla/default behavior intact.
The reason for this is that it allows users to enable the master toggle of large configs without having to set every value.


## User Overrides

Ember supports a user override file intended to survive mod updates.

## How Overrides Work
`src/User_Config_Overrides.lua` is loaded **after** all other config modules, so any variables you define there override values from `src/Config/*.lua`.

### How to enable it
1. Locate: `src/User_Config_Overrides_Template.lua`
2. Copy it in the same folder and rename the copy to `src/User_Config_Overrides.lua`.
3. Edit `src/User_Config_Overrides.lua` and add only the settings you want to override.

### Override rules
- You only need the variable name and the value.
- You do not need to copy the whole config file.
- Missing `src/User_Config_Overrides.lua` is normal and does not log an error.
  Syntax or runtime errors inside an existing override file are still reported.

### Example
- A multi example template can be found in `src/User_Config_Overrides_Template.lua`.

# Config Quick Reference

### Misc

## Debug Logging
- File: `src/Config/Debug_Level.lua`
- Value: `LOG_LEVEL` (integer)
Higher values will show more detailed logs.

**No Intro Video**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_NoIntroVideo` (boolean)

### Storage

**Expanded Magic Storage**
- File: `src/Config/Ember_Config.lua`
- Toggles:
  - `Enable_MagicFurniture` (boolean)
  - `Enable_MagicFactories` (boolean)
- Adds the current magic-storage `InventoryCraftingStock` component to
  inventory-capable storage decoration/factory templates. Templates without
  `InventorySetup` are skipped.

**Stack Size Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_StackSizeTweaks` (boolean)
- Max Stack Size: `StackSize_MaxStack` (integer)

### Exploration

**EA Barrier Removal**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_NoBarriers` (boolean)

**Slope Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_SlopeTweaks` (boolean)
- Values:
  - Walkable Angle: `steepFloorAngle` (float, degrees)
  - Sliding Angle: `slidingAngle` (float, degrees)
  - Fall Damage Angle: `fallDamageAngle` (float, degrees)
  - Max Angle: `Max_Angle` (float safety clamp)

### Progression

**Player / Item Level Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_PlayerItemLevelTweaks` (boolean)
- Optional sync toggle: `Enable_ItemLevelCapSync` (boolean)
- Values:
  - Player Max Level: `PlayerLevelMax` (int)
  - Player XP Cap: `PlayerLevelCap` (int)
  - Item Level Cap: `ItemLevelCap` (int)
  - Player Item Level Max Clamp: `PlayerItemLevel_MaxClamp` (int safety clamp)
  - Item Level Sync Safety Skip: `ItemLevelSync_SafetySkip` (string safety skip)

**Skill Points per Level**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_SkillPointsPerLevel` (boolean)
- Value: `SkillPointsPerLevel` (int)
- Safety clamp: `SkillPoints_MaxClamp` (int)
- Also applies when `Enable_PlayerItemLevelTweaks` is enabled, preserving older
  Ember configs.
- Patches `BalancingTable.skillPointsPerLevel`; test by gaining a level after
  the mod loads. It does not grant retroactive points to an already-leveled
  character.

### Spells

**Spell Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_SpellTweaks` (boolean)
- Values:
  - Cast Time Multiplier: `SpellCastTime_Multiplier` (float)
  - Mana Cost Multiplier: `SpellManaCost_Multiplier` (float)
  - Min Multiplier: `SpellTweaks_MinMultiplier` (float safety floor)
- Cast-time changes patch player spell-ammunition charge-duration values and
  matching UI display values.
- Mana-cost changes patch player spell-ammunition mana impact values and
  matching UI display values. A `0.0` multiplier also ignores `Mana` on the
  spell usage-cost sequence while preserving spell item consumption.

### Buffs

**Buff Reapplication**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_BuffReapplication` (boolean)

### Shroud

**Shroud Timer Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_ShroudTimerTweaks` (boolean)
- Shroud Time Multiplier: `ShroudTimer_Multiplier` (float)
- Clamp: `ShroudTimer_MaxMultiplierClamp` (float)

**Fog Tweaks**
- File: `src/Config/Fog_Tweaks_Config.lua`
- Toggle: `Enable_FogTweaks` (boolean)
- Values (see file for full list):
  - Ambient: `Ambient_Fog_Density`
  - Weather: `Rain_Opacity`, `Snow_Opacity`, `Blizzard_Opacity`
  - Height fog: `HeightFog_Density`, `HeightFog_Opacity`
  - Shroud: `Shroud_Fog_Dangerous_*`, `Shroud_Fog_Deadly_*`
  - Clamps: `Fog_Density_MaxClamp`, `Fog_Opacity_MaxClamp`, etc.

### Building

**Flame Altar Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_FlameAlterTweaks` (boolean)
- Values:
  - Altars-per-level multiplier: `MaxFlameAlters_Multiplier` (float)
  - Altars-per-level cap: `MaxFlameAlters_Cap` (int)
  - Altars-per-level max multiplier clamp: `FlameAltar_MaxMultiplierClamp` (float safety clamp)

**Base Size Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggle: `Enable_BaseSizeTweaks` (boolean)
- Values:
  - Base Size Multiplier: `BaseSize_Multiplier` (float)
  - Base Size Max Multiplier Clamp: `BaseSize_MaxMultiplierClamp` (float safety clamp)

**Placement / Building Tweaks**
- File: `src/Config/Ember_Config.lua`
- Toggles:
  - Master Toggle: `Enable_PlacementTweaks`
  - Remove No-Build Zones: `Enable_BuildingTweaks`
  - Build in Shroud: `PlacementTweaks_BuildInFog`
  - Place Outside Altar Zones: `PlacementTweaks_NoBuildZoneNeeded`
- Safety:
  - `PlacementTweaks_SafetySkip` (string)

### Map Tweaks

**Fast Travel Tweaks**
- File: `src/Config/Map_Tweaks_Config.lua`
- Feature: `src/Features/Map_Tweaks.lua`
- Toggle: `Enable_FastTravelTweaks` (boolean)
- Options:
  - Flame Markers: `FastTravel_FlameMarkers`
  - Dungeon Markers: `FastTravel_DungeonMarkers`
  - Location Markers: `FastTravel_LocationMarkers`
  - None: `FastTravel_None`
  - Custom Marker: `FastTravel_CustomMarker` (Currently not functional)

**Fog of War Tweaks**
- File: `src/Config/Map_Tweaks_Config.lua`
- Feature: `src/Features/Map_Tweaks.lua`
- Toggle: `Enable_FogOfWarTweaks` (boolean)
- Values:
  - Discovery Range: `FogOfWar_Range` (number)
  - Max Range Clamp: `FogOfWar_MaxRangeClamp` (number safety clamp)

### Gliders

**Glider Tuning**
- File: `src/Config/Glider_Stats_Config.lua`
- Toggle: `Enable_GliderTuning` (boolean)
- Table: `GliderConfig` (per tier: `T1`, `T2`, `T3`, `T4_REWARD`)

### Terraforming

**Terrain Replacer**
- File: `src/Config/Terrain_Replacer_Config.lua`
- Toggle: `Enable_VoxelMaterialReplacer` (boolean)
- Table: `VoxelMaterialReplacements`
  - Each entry: `{ enabled = bool, targetGuid = "GUID", toId = <MaterialID> }`
- A complete list of Material IDs is available at [Enshrouded Building Material Catalog](https://docs.google.com/spreadsheets/d/1hMTF2QMDnfnwD2lbhJtHRPj-awAU1-DUuethcbzkZB4/edit?usp=sharing)

**Block Replacer**
- File: `src/Config/Block_Replacer_Config.lua`
- Toggle: `Enable_BlockMaterialReplacer` (boolean)
- Table: `BlockMaterialReplacements`
  - Each entry: `{ enabled = bool, targetGuid = "GUID", materialIndex = <BlockID or nil> }`
- A complete list of Block IDs is available at [Enshrouded Building Material Catalog](https://docs.google.com/spreadsheets/d/1hMTF2QMDnfnwD2lbhJtHRPj-awAU1-DUuethcbzkZB4/edit?usp=sharing)

**Terraforming Tweaks**
- File: `src/Config/Terraforming_Tweaks_Config.lua`
- Toggles:
  - `Enable_TerrainDropTweaks` (boolean)
  - `Enable_TerraformingTweaks` (boolean)
- Values:
  - Per Voxel Loot Drop Rate: `TerrainDrop_ExchangeRate` (number)
- Table:
  - `TerraformingTweaks`
    `{ enabled = bool, materialId = <id>, hardness = "...", healthPoints = <int>, damageSusceptibility = "..." }`
- `TerraformingTweaks` targets terrain material IDs from the current
  `TerraformingEfficiencyRegistryResource.terrainConfigs`. Building block IDs
  belong to `BlockMaterialReplacements` and are not supported here unless a
  current dump shows them in the terraforming registry.

### Blueprints

**Blueprint Injector**
- File: `src/Config/Custom_Blueprint_Config.lua`
- Toggle: `Enable_BlueprintInjector` (boolean)
- Primary size values:
  - `BlueprintInjector_SizeX`
  - `BlueprintInjector_SizeY`
  - `BlueprintInjector_SizeZ`

### Expanded Game Settings

**Expanded Game Settings**
- File: `src/Config/ExpandedGameSettings_Config.lua`
- Toggle: `Enable_ExpandedGameSettings` (boolean)
- Table: `ExpandedGameSettings_Config`
  - Each entry includes: `Name`, `Min`, `Max`, `Steps`, `Enabled`
- Boolean entries use `Type = "Boolean"` and `Value = true/false`.
- Durability is supported through the current `enableDurability` boolean
  setting. The old scalar `durability` row is not supported.

### Skills

**Updraft skill tuning**
- File: `src/Config/SkillTweaks_Updraft_Config.lua`
- Toggle: `Enable_SkillTweaks_Updraft` (boolean)
- Values (all default to `nil` meaning unchanged):
  - Ground-relative: `GroundRelative_X`, `GroundRelative_Y`, `GroundRelative_Z`
  - Player-relative: `PlayerRelative_X`, `PlayerRelative_Y`, `PlayerRelative_Z`
  - `Boost_Duration`

### Gems

**Gem Tweaks**
- File: `src/Config/Gem_Tweaks_Config.lua`
- Toggle: `Enable_GemTweaks` (boolean)
- Values:
  - `GemTweaks_SalvageRate`
  - Slot probabilities by rarity:
    - `GemTweaks_Probability_Common`
    - `GemTweaks_Probability_Uncommon`
    - `GemTweaks_Probability_Rare`
    - `GemTweaks_Probability_Epic`
    - `GemTweaks_Probability_Legendary`

### Crafting

**Crafting Tweaks**
- File: `src/Config/Crafting_Tweaks_Config.lua`
- Toggle: `Enable_CraftingTweaks` (boolean)
- Options:
  - Unlock All Recipes: `CraftingTweaks_UnlockAllRecipes`
  - Remove Crafting Costs: `CraftingTweaks_CreativeMode`
- `CraftingTweaks_UnlockAllRecipes` uses the current recipe knowledge
  requirement shape with the base-started bool knowledge used by legacy
  unlock-all mods.
- `CraftingTweaks_CreativeMode` keeps recipe input rows and sets item/category
  counts to 0.

### Fishing

**Fishing Tweaks**
- File: `src/Config/Fishing_Tweaks_Config.lua`
- Toggle: `Enable_Fishing_Tweaks` (boolean)
- Per Tier Values:
  - `Tier1_*` (common)
  - `Tier2_*` (uncommon)
  - `Tier3_*` (rare)
  - `Tier4_*` (epic)
- Properties Include:
  - `RodStrength`, `RodEndurance`, `QuickTimeEvent`, `AdvancedGame`, `ReduceRoundsBy`

### Loot

**Loot / Resource Drop Tweaks**
- File: `src/Config/Loot_Tweaks_Config.lua`
- Feature: `src/Features/Loot_Tweaks.lua`
- Toggle: `Enable_LootTweaks` (boolean)
- Values:
  - Resource drop multiplier: `Loot_ResourceDropMultiplier` (number)
  - Item/loot drop multiplier: `Loot_ItemDropMultiplier` (number)
  - Max stack size (loot tables): `Loot_MaxStackSize` (int)
  - Multiplier clamp: `Loot_DropMultiplierMaxClamp` (number safety clamp)
  - Max stack clamp: `Loot_MaxStackSizeClamp` (int safety clamp)
