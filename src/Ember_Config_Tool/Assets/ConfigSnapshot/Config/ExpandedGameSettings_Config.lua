-- ========================================
--        Expanded Game Settings Config
-- ========================================

--[[

[Configuration Table]
'Min' / 'Max' are percentage multipliers (10 = 10%, 100 = 100%, 1000 = 1000%)
'Steps' is the number of steps between Min and Max (including Min and Max)
Boolean rows use Value = true/false.

--]]

-- Master Toggle
Enable_ExpandedGameSettings = true

ExpandedGameSettings_Config = {
-- [PLAYER]
    -- Health
    { Name = "playerHealth",         Min = 10, Max = 1000, Steps = 20, Enabled = true},
    -- Mana
    { Name = "playerMana",           Min = 10, Max = 1000, Steps = 20, Enabled = true},
    -- Stamina
    { Name = "playerStamina",        Min = 10, Max = 1000, Steps = 20, Enabled = true},
    -- Body Heat
    { Name = "bodyHeat",             Min = 10, Max = 1000, Steps = 20, Enabled = true},

-- [SURVIVAL]
    -- Consumable Buff Duration
    { Name = "foodDuration",         Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Time to Starvation
    { Name = "starvingTime",         Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Breath Timer
    { Name = "playerDivingTime",     Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Shroud Time
    { Name = "shroudTime",           Min = 10, Max = 1000, Steps = 20, Enabled = false},

-- [WORLD]
    -- Mining Damage
    { Name = "miningDamage",         Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Plant Growth Time
    { Name = "plantGrowTime",        Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Drop Amount
    { Name = "dropAmount",           Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Production Time
    { Name = "productionTime",       Min = 10, Max = 1000, Steps = 20, Enabled = false},

-- [COMBAT]
    -- Weapon Upgrade Cost
    { Name = "weaponUpgradeCost",    Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Recycling Material Return Rate
    { Name = "perkUpgradeRecyclingFactor", Min = 10, Max = 10000, Steps = 21, Enabled = false},
    -- Durability
    { Name = "enableDurability",     Value = true, Type = "Boolean", Enabled = false},

-- [XP]
    -- Combat XP
    { Name = "combatXp",             Min = 0,  Max = 1000, Steps = 21, Enabled = false},
    { Name = "miningXp",             Min = 0,  Max = 1000, Steps = 21, Enabled = false},
    { Name = "questXp",              Min = 0,  Max = 1000, Steps = 21, Enabled = false},

-- [ENEMY]
    -- Enemy Damage
    { Name = "enemyDamage",          Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Enemy Health
    { Name = "enemyHealth",          Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Enemy Stamina
    { Name = "enemyStamina",         Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Enemy Perception Range
    { Name = "enemyPerceptionRange", Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Enemy Attack Frequency
    { Name = "enemyAttackFrequency", Min = 10, Max = 1000, Steps = 20, Enabled = false},

-- [BOSS]
    -- Boss Damage Multiplier
    { Name = "bossDamage",           Min = 10, Max = 1000, Steps = 20, Enabled = false},
    -- Boss Health Multiplier
    { Name = "bossHealth",           Min = 10, Max = 1000, Steps = 20, Enabled = false},

-- [TIME]
    -- Day Length *in Minutes*
    { Name = "dayTime",              Min = 1.0, Max = 120.0, Steps = 30, Type = "Scalar", Enabled = false },
    -- Night Length *in Minutes*
    { Name = "nightTime",            Min = 1.0, Max = 120.0, Steps = 30, Type = "Scalar", Enabled = false },
}
