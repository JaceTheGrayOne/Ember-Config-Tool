--[[

[User Config Overrides Template]

***Copy this file in place and rename it to User_Config_Overrides.lua.***

User_Config_Overrides.lua will override any config options set in the config files in /src/config/.
It is loaded last, so it will always take precedence.
Config options stored there will be preserved across updates.

To add config options to this file you only need the config option name and value.

As in the example below you can include the headers and section separates or you can just stack the options.

--]]

-- ========================================
--                 Misc
-- ========================================

-- No Intro Video
Enable_NoIntroVideo = true

Enable_StackSizeTweaks = true
StackSize_MaxStack = 65535

Enable_ExpandedGameSettings = true
ExpandedGameSettings_Config = {
    { Name = "playerMana", Min = 10, Max = 1000, Steps = 20, Enabled = true},
    { Name = "playerStamina", Min = 10, Max = 1000, Steps = 20, Enabled = true},
}
