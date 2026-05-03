-- ========================================
--            UPDRAFT CONFIG
-- ========================================

-- Master Toggle
Enable_SkillTweaks_Updraft = false

-- ----------------------------------------
--        Boost Characteristics
-- ----------------------------------------
--[[

Below is a set of boost characteristics that can be modified for the Updraft Skill.
The game uses two controls for this. A "ground relative" set and a "player relative" set.
The easiest way I can think to explain this is this:
A positive "up" (ground relative) value will cause you to move upwards regardless of your player's orientation.
A positive "up" (player relative) value will cause you to move upwards if your player is gliding flatly horziontal, or if your player is aimed downward it will push you forward.
Basically ground relative always moves you in an absolute direction, player relative moves you in a direction relative to the players orientation.

--]]

-- Ground Relative
-- Down (Absolute)
-- Default: 0
GroundRelative_X  = nil

-- Up (Absolute)
-- Default: 22
GroundRelative_Y  = 0

-- Forward (Absolute)
-- Default: 0
GroundRelative_Z  = nil

-- Player Relative
-- [+] Right / [-] Left (Relative to player's roll and yaw)
-- Default: 0
PlayerRelative_X = nil

-- [+] Upward / [-] Downward (Relative to player's pitch and yaw)
-- Default: 0
PlayerRelative_Y = 22

-- [+] Forward / [-] Backward (Relative to player's pitch and yaw)
-- Default: 0
PlayerRelative_Z = nil

-- Duration (in seconds)
-- Default: 2.25
Boost_Duration = nil

-- ----------------------------------------
--              Mana Cost
-- ----------------------------------------
-- Disable Updraft mana cost.
Updraft_IgnoreManaCost = false

-- Override Updraft mana cost.
Updraft_ManaCost_Value = nil
