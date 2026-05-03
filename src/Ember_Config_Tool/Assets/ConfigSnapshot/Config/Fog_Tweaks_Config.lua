-- ========================================
--          FOG TWEAKS CONFIG
-- ========================================

--[[

Density = Fog thickness
Opacity = Light penetration through fog
Exposed_Emissive_Intensity = Brightness of the exterior edge of the fog as seen from outside the fog
Emissive_Intensity = Brightness of the edge of the fog against things while inside the fog
Scatter_Boost = This one is hard to explain, it causes higher light diffusion through the fog which basically just makes it harder to see.

]]

-- Master Toggle
Enable_FogTweaks = false

-- ----------------------------------------
--               Ambient Fog
-- ----------------------------------------
-- This is the fog you see pretty much everywhere
-- Default: 8.0
Ambient_Fog_Density = 8.0

-- ----------------------------------------
--              Weather Fog
-- ----------------------------------------
-- This is the fog you see when it's raining, snowing, or blizzarding
-- Default for all: 0.01
Rain_Opacity = 0.01
Snow_Opacity = 0.01
Blizzard_Opacity = 0.01

-- ----------------------------------------
--              Height Fog
-- ----------------------------------------
-- This is the fog you see when looking down from high up
-- Default: 0.0012
HeightFog_Density = 0.0012

-- Default: 0.15
HeightFog_Opacity = 0.15

-- ----------------------------------------
--                   Fog
-- ----------------------------------------
-- ???????????
-- Default: 0.3
Fog_M1_Density = 0.3

-- Default: 0.1
Fog_M1_Opacity = 0.1

-- ----------------------------------------
--     Dangerous (Blue) Shroud Fog
-- ----------------------------------------

-- Density
-- Default: 1.0
Shroud_Fog_Dangerous_Density = 1.0

-- Opacity
-- Default: 0.35
Shroud_Fog_Dangerous_Opacity = 0.35

-- NOTE: "Barrier" fog refers to the fog at the interface between the shroud and objects or the outside world

-- Barrier Exterior Edge Brightness
-- Default: 0.1
Dangerous_Fog_Barrier_Exposed_Emissive_Intensity = 0.1

-- Barrier Interior Edge Brightness
-- Default: 0.0
Dangerous_Fog_Barrier_Emissive_Intensity = 0.0

-- Barrier Interior Light Penetration
-- Default: 0.3
Dangerous_Fog_Barrier_Opacity = 0.3

-- Barrier Light Diffusion
-- Default: 2.0
Dangerous_Fog_Barrier_Scatter_Boost = 2.0

-- ----------------------------------------
--    Deadly (Red) Shroud Fog
-- ----------------------------------------

-- Density
-- Default: 1.0
Shroud_Fog_Deadly_Density = 1.0

-- Opacity
-- Default: 0.5
Shroud_Fog_Deadly_Opacity = 0.5

-- NOTE: "Barrier" fog refers to the fog at the interface between the shroud and objects or the outside world

-- Barrier Exterior Edge Brightness
-- Default: 0.2
Deadly_Fog_Barrier_Exposed_Emissive_Intensity = 0.2

-- Barrier Interior Edge Brightness
-- Default: 0.0
Deadly_Fog_Barrier_Emissive_Intensity = 0.0

-- Barrier Interior Light Penetration
-- Default: 0.5
Deadly_Fog_Barrier_Opacity = 0.5

-- Barrier Light Diffusion
-- Default: 2.0
Deadly_Fog_Barrier_Scatter_Boost = 2.0

-- ----------------------------------------
--              Safety Clamps
-- ----------------------------------------
Fog_Density_MaxClamp = 100.0
Fog_Opacity_MaxClamp = 100.0
Fog_Emissive_MaxClamp = 100.0
Fog_Scatter_MaxClamp = 20.0
