-- ========================================
--                 Glider
-- ========================================

-- Master Toggle
Enable_GliderTuning = false

GliderConfig = {
    -- Glider Tier 1
    T1 = {
        accelerationForward       = 0.7, -- default: 0.8   | Acceleration (Higher = Max speed faster | Lower = Max speed slower)
        airResistanceLongitudinal = 0.4, -- default: 0.39  | Forward Air Friction (Higher = Slower | Lower = Faster)
        airResistanceLateral      = 1.8, -- default: 1.8   | Horizontal Air Friction (Lower = Air sliding)
        airResistanceVertical     = 0.8, -- default: 0.71  | Vertical Resistance (Higher = Lose altitude slower | Lower = Lose altitude faster)
        yawAngleSpeed             = 50,  -- default: 70.0  | Turn Rate (Higher = turn faster | Lower = turn slower)
        pitchAngleSpeed           = 70,  -- default: 85.0  | Pitch Rate (Higher = pitch up/down faster | Lower = pitch up/down slower)
        rollAngleSpeed            = 150, -- default: 160.0 | Bank Angle (Higher = Steeper banks | Lower = Shallow banks)
    },

    -- Glider Tier 2
    T2 = {
        accelerationForward       = 0.8, -- default: 0.9   | Acceleration (Higher = Max speed faster | Lower = Max speed slower)
        airResistanceLongitudinal = 0.3, -- default: 0.26  | Forward Air Friction (Higher = Slower | Lower = Faster)
        airResistanceLateral      = 1.7, -- default: 1.7   | Horizontal Air Friction (Lower = Air sliding)
        airResistanceVertical     = 1.2, -- default: 0.95  | Vertical Resistance (Higher = Lose altitude slower | Lower = Lose altitude faster)
        yawAngleSpeed             = 60,  -- default: 65.0  | Turn Rate (Higher = turn faster | Lower = turn slower)
        pitchAngleSpeed           = 75,  -- default: 80.0  | Pitch Rate (Higher = pitch up/down faster | Lower = pitch up/down slower)
        rollAngleSpeed            = 135, -- default: 155.0 | Bank Angle (Higher = Steeper banks | Lower = Shallow banks)
    },

    -- Glider Tier 3
    T3 = {
        accelerationForward       = 0.9, -- default: 1.0   | Acceleration (Higher = Max speed faster | Lower = Max speed slower)
        airResistanceLongitudinal = 0.2, -- default: 0.215 | Forward Air Friction (Higher = Slower | Lower = Faster)
        airResistanceLateral      = 1.6, -- default: 1.6   | Horizontal Air Friction (Lower = Air sliding)
        airResistanceVertical     = 2.0, -- default: 1.6   | Vertical Resistance (Higher = Lose altitude slower | Lower = Lose altitude faster)
        yawAngleSpeed             = 70,  -- default: 60.0  | Turn Rate (Higher = turn faster | Lower = turn slower)
        pitchAngleSpeed           = 80,  -- default: 75.0  | Pitch Rate (Higher = pitch up/down faster | Lower = pitch up/down slower)
        rollAngleSpeed            = 115, -- default: 150.0 | Bank Angle (Higher = Steeper banks | Lower = Shallow banks)
    },

    -- Glider Tier 4
    T4_REWARD = {
        accelerationForward       = 1.0,  -- default: 1.0   | Acceleration (Higher = Max speed faster | Lower = Max speed slower)
        airResistanceLongitudinal = 0.15, -- default: 0.155 | Forward Air Friction (Higher = Slower | Lower = Faster)
        airResistanceLateral      = 1.5,  -- default: 1.5   | Horizontal Air Friction (Lower = Air sliding)
        airResistanceVertical     = 3.0,  -- default: 3.0   | Vertical Resistance (Higher = Lose altitude slower | Lower = Lose altitude faster)
        yawAngleSpeed             = 80,   -- default: 55.0  | Turn Rate (Higher = turn faster | Lower = turn slower)
        pitchAngleSpeed           = 85,   -- default: 70.0  | Pitch Rate (Higher = pitch up/down faster | Lower = pitch up/down slower)
        rollAngleSpeed            = 100,  -- default: 100.0 | Bank Angle (Higher = Steeper banks | Lower = Shallow banks)
    },
}