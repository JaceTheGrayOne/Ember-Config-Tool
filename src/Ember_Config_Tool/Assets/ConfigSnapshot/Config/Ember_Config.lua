-- ========================================
--                 Misc
-- ========================================

-- No Intro Video
Enable_NoIntroVideo = false

-- ========================================
--                 Storage
-- ========================================

-- ---------------
--  Magic Storage
-- ---------------
-- Adds the current magic-storage crafting-stock component to inventory-capable
-- storage decoration/factory templates. Templates without InventorySetup are
-- skipped.
-- Storage decoration magic storage
Enable_MagicFurniture = false

-- Production building magic storage
Enable_MagicFactories = false

-- ---------------
--   Stack Size
-- ---------------
-- Stack Size Tweaks
Enable_StackSizeTweaks = false

-- Max Stack Size
-- Default = Item Specific
StackSize_MaxStack = 65535

-- ========================================
--                 Exploration
-- ========================================

-- ---------------
--   Barriers
-- ---------------

-- Early Access Barrier Removal
Enable_NoBarriers = false

-- ---------------
--    Slopes
-- ---------------

-- Slope Tweaks
Enable_SlopeTweaks = false

-- Angle at which terrain becomes difficult to walk up
-- Default = 45 | Recommended = 48
steepFloorAngle = 45.0

-- Angle at which you will start to slide off terrain
-- Default = 55 | Recommended = 62
slidingAngle = 55.0

-- Angle at which you will take fall damage from sliding down terrain onto flat ground
-- Default = 65 | Recommended = 70
fallDamageAngle = 65.0

-- Safety clamp to prevent instability or corruption
Max_Angle = 89.0

-- ========================================
--                 Progression
-- ========================================

-- ---------------------
--  Player / Item Level
-- ---------------------

-- Master Toggle
Enable_PlayerItemLevelTweaks = false

-- Sync between the global item cap and the item specific cap
Enable_ItemLevelCapSync = false

-- Maximum possible player level
-- Default = 45
PlayerLevelMax = 45

-- Level at which player stops earning XP
-- Default = 45
PlayerLevelCap = 45

-- Max item level
-- Default = 50
ItemLevelCap   = 50

-- Safety clamp to prevent instability or corruption
PlayerItemLevel_MaxClamp = 150

-- Item safety skip to avoid crashes
ItemLevelSync_SafetySkip = "Material"

-- ---------------
--  Skill Points
-- ---------------

-- Standalone skill points toggle.
-- Enable_PlayerItemLevelTweaks also applies this value for older configs.
Enable_SkillPointsPerLevel = false

-- Amount of skill points acquired each levelup.
-- Applies to future level-ups after the mod loads.
-- Default = 2
SkillPointsPerLevel = 2

-- Safety clamp to prevent instability or corruption
SkillPoints_MaxClamp = 15

-- ========================================
--                 Spells
-- ========================================

-- Master Toggle
Enable_SpellTweaks = false

-- Multiplies the spell charge time before release.
-- Default: 1.0
SpellCastTime_Multiplier = 1.0

-- Multiplies Spell mana cost
-- Set to 0.0 to ignore Mana costs while preserving spell item consumption.
-- Default: 1.0
SpellManaCost_Multiplier = 1.0

-- Safety clamp to prevent instability or corruption
SpellTweaks_MinMultiplier = 0.0

-- ========================================
--                 Buffs
-- ========================================

-- Enables consumable buffs to be re-applied before they have expired
Enable_BuffReapplication = false

-- ========================================
--             Shroud Timer
-- ========================================

-- Master Toggle
Enable_ShroudTimerTweaks = false

-- Multiplies base shroud resistance time
-- Default: 0.0
ShroudTimer_Multiplier = 20.0

-- Safety clamp to prevent instability or corruption
ShroudTimer_MaxMultiplierClamp = 10000.0

-- ========================================
--                 Flame Altar
-- ========================================

-- Master Toggle
Enable_FlameAltarTweaks = false

-- Multiplies amount of buildable Flame Altars per Flame level
-- Default: 2.0
MaxFlameAltars_Multiplier = 2.0

-- Max number of Flame Altars
-- Default: 20
MaxFlameAltars_Cap = 20

-- Safety clamp to prevent instability or corruption
FlameAltar_MaxMultiplierClamp = 3.0

-- ========================================
--                 Base Building
-- ========================================

-- ---------------
--  Base Size
-- ---------------
-- Master Toggle
Enable_BaseSizeTweaks = false

-- Multiplies each buildzoneSizesPerAltarLevel (x,y,z) where x > 0.
-- Default: 1.0
BaseSize_Multiplier = 3.0

-- Safety clamp to prevent instability or corruption
BaseSize_MaxMultiplierClamp = 3.0

-- ------------------
--  Placement Tweaks
-- ------------------
-- Master toggle
Enable_PlacementTweaks = false

-- Removes all "No Build Zones"
Enable_BuildingTweaks = false

-- Enables building placement in Shroud fog
PlacementTweaks_BuildInFog = false

-- Enables placement outside of Flame Altar zones
PlacementTweaks_NoBuildZoneNeeded = false

-- Item safety skip to avoid crashes
PlacementTweaks_SafetySkip = "Material"
