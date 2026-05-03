-- ========================================
--          Custom Blueprints
-- ========================================

-- Master Toggle
Enable_BlueprintInjector = false

-- New Blueprint Dimensions
-- X-Axis (Front to Back) | Depth (From player perspective)
BlueprintInjector_SizeX = 8

-- Y-Axis (Top to Bottom) | Height (From player perspective)
BlueprintInjector_SizeY = 8

-- Z-Axis (Left to Right) | Width (From player perspective)
BlueprintInjector_SizeZ = 8


-- !!! DON'T TOUCH ANYTHING BELOW HERE UNLESS YOU KNOW WHAT YOU'RE DOING !!!
-- Safety Clamp | Clamp to prevent instability or corruption
BlueprintInjector_MaxVolume = 4096

-- Selectors
-- Target master hologram to revert to
BlueprintInjector_MasterHologramGuid = "67a477d6-4ac5-4fe4-aae0-2b2e473bdea3"

-- GUID prefix of the template to clone
BlueprintInjector_TemplateGuidPrefix = "b53b"

-- ItemInfo template to clone for the new blueprint item
BlueprintInjector_BaseBlueprintDebugName = "Blueprint_Voxel_Block_Wall_Straight_4m"

-- New Blueprint
-- Item ID for the injected blueprint (This is an arbitrary integer from 1 - 100000)
BlueprintInjector_NewItemId = 999991

-- ItemInfo.debugName for the injected blueprint
BlueprintInjector_NewDebugName = "Debug_Integer_3"

-- Blueprint Placement Alignment Restoration
-- Voxels to Meters scale used to compute AABB max = size * scale (min at 0)
BlueprintInjector_VoxelsToMeters = 0.5

-- Vanilla Reversion Parameters (for master hologram)
BlueprintInjector_RevertSizeX = 8
BlueprintInjector_RevertSizeY = 8
BlueprintInjector_RevertSizeZ = 1
BlueprintInjector_RevertFillValue = 255

--[[

TLDR: This injects a second "Wide Wall" in the 4m section of the Construction Hammer.
      If you want to use it easily treat the XYZ axes as a cube and make it whatever size you want.
      Anything larger than 32x32x32 will crash your game.
      Custom shape generation may be expanded later; the current settings cover simple cuboid blueprints.

Voxel Grid Summary
dimensions: x=8 | y=8 | z=8
axes: x: front to back | y: bottom to top | z: right to left
index = ((z-1)*64)+((y-1)*8)+x (min: 1 | max: 512)

Voxel Coordinate System
Each voxel is identified by integer coordinates (x, y, z):
x ∈ [1..8] → Front (1) to Back (8)
y ∈ [1..8] → Bottom (1) to Top (8)
z ∈ [1..8] → Right (1) to Left (8)
#- (∈ = "is an element of"; here it means x, y, and z may each take any integer value from 1 to 8)

Voxel indices are assigned using the following rule:
index = ((z - 1) * 64) + ((y - 1) * 8) + x

x increases left-to-right within a row
y increases bottom-to-top across rows
z increases right-to-left across vertical layers
Index 1 is the furthest-bottom-front-right voxel
Index 512 is the closest-top-back-left voxel

Layer Dimensions
X = 8 (Forward <-> Back depth from user’s perspective)  → columns
Y = 8 (Up <-> Down height from user’s perspective)      → rows
Z = 1 (Left <-> Right thickness)                        → single vertical slice

Vertical Z-Axis (Left to Right) Layer Ordering
| Layer 08 | Layer 07 | Layer 06 | Layer 05 | Layer 04 | Layer 03 | Layer 02 | Layer 01 |

Horizontal Y-Axis (Top to Bottom) Layer Ordering
| Layer 08 |
| Layer 07 |
| Layer 06 |
| Layer 05 |
| Layer 04 |
| Layer 03 |
| Layer 02 |
| Layer 01 |

## Depth X-Axis (Front to Back) Layer Ordering
| Layer 08 | Layer 07 | Layer 06 | Layer 05 | Layer 04 | Layer 03 | Layer 02 | Layer 01 |

]]
