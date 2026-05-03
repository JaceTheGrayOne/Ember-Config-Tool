
-- ========================================
--          Terraforming Tweaks
-- ========================================

-- ----------------------------------------
--       Terraforming Drop Rate
-- ----------------------------------------
-- Enables terrain item drop modification
Enable_TerrainDropTweaks = false

-- Ratio of items dropped per voxel destroyed.
-- 1.0 = 1 item per voxel destroyed.
-- Values above 1.0 have no effect.
-- Default: 0.2
TerrainDrop_ExchangeRate = 1.0

-- ----------------------------------------
--       Terraforming Properties
-- ----------------------------------------
-- Enables the editing of terrain material properties present in
-- TerraformingEfficiencyRegistryResource.terrainConfigs
Enable_TerraformingTweaks = false

-- Enabled | Target Material ID | Hardness | HP | Damage Susceptibility
-- ex. { enabled = true, materialId = 34, hardness = "Hard", healthPoints = 20, damageSusceptibility = "Stone" }, -- Set Lava hardness to Hard, HP to 20, and Susceptibility to Stone
-- Target Material IDs must exist in the current terraforming registry.
TerraformingTweaks = {

    -- Terrain
    { enabled = true, materialId = 34, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Lava
    { enabled = true, materialId = 5, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Bedrock
    { enabled = true, materialId = 16, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Red Shroud Essence
    { enabled = true, materialId = 94, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Shroud Slime

    -- Block material IDs are not current terrainConfigs entries. These rows are
    -- retained disabled until a current block-material hardness source is found.
    { enabled = false, materialId = 154, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Ancient Brick Block
    { enabled = false, materialId = 157, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Dirty Ancient Tile
    { enabled = false, materialId = 160, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Ancient Golden Block
    { enabled = false, materialId = 175, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Red Accent Block
    { enabled = false, materialId = 176, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Fancy Wood
    { enabled = false, materialId = 183, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Ancient Tile
    { enabled = false, materialId = 185, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Mossy Ancient Tile
    { enabled = false, materialId = 200, hardness = "VeryHard", healthPoints = 50, damageSusceptibility = "Stone" }, -- Starlight Block
}

--[[

FULL TERRAIN PROPERTY LIST:
https://docs.google.com/spreadsheets/d/1hMTF2QMDnfnwD2lbhJtHRPj-awAU1-DUuethcbzkZB4/edit?usp=sharing


[Hardness]
Soft (Dirt, Rubble, Soil, Snow, etc...)
Slightly Hard (Rubble, Stone, Mycelium, Bone, Fossil, etc...)
Moderately Hard (Limestone Shells, Salt, Amber, FieldStone, Ice, etc...)
Hard (Amethyst, Lapislazui, Red Marble, Aquamarine, etc...)
Very Hard (Copper, Tin, Iron, Silver, Gold, Granite, Tar, Obsidian)
Unbreakable (Lava)

[Health Points]
0-8: Dirt, Snow, Sand, Rubble, Stone, Clay, Gems, Ore, Bones, Mycelium, etc...
10-16: Moss, Granite, Fossilized Bone, Hardwood, Active Mycelium, Red Marble, etc...
20-25: Tree Bark, Underwater Mud, Gold, Silver, Obsidian, etc...
30: Granite Road
60: Toxic Goo
100: Tar
500: Lava

[Damage Susceptibility]
Honestly I don't know what the difference is between Stone/Wood/Metal. Their data seems the same. They all work.
None  = Cannot be mined
Stone = Can be mined
Wood  = Can be mined
Metal = Can be mined

[Useful Material IDs]
These are terrain registry IDs for TerraformingTweaks. Building block IDs are
handled by Block_Replacer_Config.lua and are not valid here unless current dump
evidence shows them in the terraforming registry.

5 Bedrock
34 Lava
37 Mud
51 Tar
73 Mud (Possible Duplicate?)
94 Shroud Slime
16 Red Shroud Essence

--]]


