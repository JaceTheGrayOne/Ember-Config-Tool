-- ========================================
--           FISHING TWEAKS CONFIG
-- ========================================
--[[

[Config]
nil = Does not patch this value. This leaves the default value intact and does not interact with it at all.

[Fishing Tiers]
As far as I can tell the "tier" of the fishing minigame is determined
by the fish's rarity, but this is a straight up guess. I had a lot of difficulty
trying to confirm this because the results of testing were inconsistent.

If you figure this out PLEASE let me know so I can update this.

[Fishing Game Properties]
RodStrength    | Stamina drained from the fish when you pull
RodEndurance   | Stamina drained from you when the fish pulls
QuickTimeEvent | Time you have to react to "hook" and "catch" events
AdvancedGame   | Bonus rounds enabled/disabled
ReduceRoundsBy | Bonus rounds reduced overall (set to ~10 for instant catching)

--]]

-- Master Toggle
Enable_Fishing_Tweaks = false

-- ---------------
--   Common Fish
-- ---------------
Tier1_RodStrength    = nil -- Default = 1.5
Tier1_RodEndurance   = nil -- Default = 1.5
Tier1_QuickTimeEvent = nil -- Default = 1.5
Tier1_AdvancedGame   = nil -- Default = false
Tier1_ReduceRoundsBy = nil -- Default = 0

-- ----------------
--  Uncommon Fish
-- ----------------
Tier2_RodStrength    = nil -- Default = 1.25
Tier2_RodEndurance   = nil -- Default = 1.25
Tier2_QuickTimeEvent = nil -- Default = 1.25
Tier2_AdvancedGame   = nil -- Default = true
Tier2_ReduceRoundsBy = nil -- Default = 0

-- ---------------
--   Rare Fish
-- ---------------
Tier3_RodStrength    = nil -- Default = 1.0
Tier3_RodEndurance   = nil -- Default = 1.0
Tier3_QuickTimeEvent = nil -- Default = 1.25
Tier3_AdvancedGame   = nil -- Default = true
Tier3_ReduceRoundsBy = nil -- Default = 0

-- ---------------
--    Epic Fish
-- ---------------
Tier4_RodStrength    = nil -- Default = 0.9
Tier4_RodEndurance   = nil -- Default = 1.0
Tier4_QuickTimeEvent = nil -- Default = 1.0
Tier4_AdvancedGame   = nil -- Default = true
Tier4_ReduceRoundsBy = nil -- Default = 0

-- ----------------
--  Legendary Fish
-- ----------------
Tier5_RodStrength    = nil -- Default = 0.9
Tier5_RodEndurance   = nil -- Default = 0.8
Tier5_QuickTimeEvent = nil -- Default = 0.8
Tier5_AdvancedGame   = nil -- Default = true
Tier5_ReduceRoundsBy = nil -- Default = 0
