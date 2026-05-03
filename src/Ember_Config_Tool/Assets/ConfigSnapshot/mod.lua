-- ========================================
--           EMBER MOD LOADER
-- ========================================

local function safe_require(name)
    local ok, result = pcall(require, name)
    if not ok then
        print("[EMBER] Error: require failed: " .. tostring(name) .. " :: " .. tostring(result))
        return nil
    end
    return result
end

local function is_module_not_found(name, err)
    local text = tostring(err or "")
    return string.find(text, "module '" .. tostring(name) .. "' not found", 1, true) ~= nil
        or string.find(text, 'module "' .. tostring(name) .. '" not found', 1, true) ~= nil
end

local function optional_require(name)
    local ok, result = pcall(require, name)
    if ok then
        return result
    end

    if is_module_not_found(name, result) then
        return nil
    end

    print("[EMBER] Error: optional require failed: " .. tostring(name) .. " :: " .. tostring(result))
    return nil
end

local function safe_call_feature(name, fn)
    local ok, err = pcall(fn)
    if not ok then
        if type(log_line) == "function" then
            log_line("ERROR in feature module: " .. tostring(name) .. " :: " .. tostring(err), 1)
        else
            print("[EMBER] ERROR in feature module: " .. tostring(name) .. " :: " .. tostring(err))
        end
    end
end

-- Load Config
safe_require("Config.Ember_Config")
safe_require("Config.Debug_Level")
safe_require("Config.Glider_Stats_Config")
safe_require("Config.Block_Replacer_Config")
safe_require("Config.Terrain_Replacer_Config")
safe_require("Config.Terraforming_Tweaks_Config")
safe_require("Config.Custom_Blueprint_Config")
safe_require("Config.ExpandedGameSettings_Config")
safe_require("Config.Fog_Tweaks_Config")
safe_require("Config.Map_Tweaks_Config")
safe_require("Config.SkillTweaks_Updraft_Config")
safe_require("Config.Gem_Tweaks_Config")
safe_require("Config.Crafting_Tweaks_Config")
safe_require("Config.Fishing_Tweaks_Config")
safe_require("Config.Loot_Tweaks_Config")
optional_require("User_Config_Overrides")


-- Load Utilities and Logger
safe_require("Logging")
safe_require("Utilities")

if type(log_line) == "function" then
    log_line("▼▼▼ EMBER ▼▼▼", 1)
end

-- Feature modules
local modules = {
    "Features.No_Intro",
    "Features.Stack_Size_Tweaks",
    "Features.Terrain_Replacer",
    "Features.Block_Replacer",
    "Features.Terraforming_Tweaks",
    "Features.Glider_Tweaks",
    "Features.Slope_Tweaks",
    "Features.Buff_Tweaks",
    "Features.Extended_Magic_Storage",
    "Features.No_Barriers",
    "Features.Building_Zone_Size",
    "Features.Flame_Altar_Tweaks",
    "Features.Building_Tweaks",
    "Features.Shroud_Tweaks",
    "Features.Progression_Tweaks",
    "Features.Spell_Tweaks",
    "Features.Custom_Blueprints",
    "Features.ExpandedGameSettings",
    "Features.Fog_Tweaks",
    "Features.SkillTweaks_Updraft",
    "Features.Map_Tweaks",
    "Features.Loot_Tweaks",
    "Features.Gem_Tweaks",
    "Features.Crafting_Tweaks",
    "Features.Fishing_Tweaks",



}

for _, name in ipairs(modules) do
    local result = safe_require(name)
    if type(result) == "function" then
        safe_call_feature(name, result)
    end
end

if type(log_line) == "function" then
    log_line("▲▲▲ EMBER ▲▲▲", 1)
end
