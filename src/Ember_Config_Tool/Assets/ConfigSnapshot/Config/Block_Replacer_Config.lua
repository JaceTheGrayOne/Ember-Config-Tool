-- ========================================
--          Block Replacer
-- ========================================
--[[

[Config]
nil = Does not patch this value. This leaves the default value intact and does not interact with it at all.

--]]

-- Master Toggle
Enable_BlockMaterialReplacer = false

-- Enabled | Replace Material GUID | With Material ID
-- ex. { enabled = true,  targetGuid = "cb7c6742-3d5f-458e-84d7-1bbfed5d0dd9", toId = 174 }, -- Replace Wood Timberframe with Bones
BlockMaterialReplacements = {
     -- Rough Stone | Blue Test Wall
    { enabled = false, targetGuid = "bea90ffd-00fb-4737-9d57-f87d7da49ca8", materialIndex = 183 },

     -- Rough Wood | Green Test Block
    { enabled = false, targetGuid = "63524189-7473-4437-b366-0a0462afa1f9", materialIndex = 16 },

     -- Metal Block | White Test Block
    { enabled = false, targetGuid = "1ae57631-0761-4f83-8e98-4406a95e4aa2", materialIndex = 34 },

     -- Granite Block | Red Test Block
    { enabled = false, targetGuid = "896ab2d5-58f7-4711-a313-a0b975a9a5a2", materialIndex = 94 },

     -- Material | Replacement
    { enabled = false, targetGuid = "", materialIndex = nil },

     -- Material | Replacement
    { enabled = false, targetGuid = "", materialIndex = nil },

     -- Material | Replacement
    { enabled = false, targetGuid = "", materialIndex = nil },
}


--[[

FULL BLOCK ID LIST:
https://docs.google.com/spreadsheets/d/1hMTF2QMDnfnwD2lbhJtHRPj-awAU1-DUuethcbzkZB4/edit?usp=sharing


[Default Blocks]
ID  | Name        | GUID
143 | Rough Wood  | 63524189-7473-4437-b366-0a0462afa1f9
155 | Rough Stone | bea90ffd-00fb-4737-9d57-f87d7da49ca8

[Test Color]
ID  | Name
129 | Blue Test Wall
131 | Green Test Block
134 | White Test Block
156 | Red Test Block

[Test Wood]
ID  | Name
137 | Red Test Wood
139 | Red Test Trim
175 | Red Accent Block
176 | Fancy Wood

[Ancient Blocks]
ID  | Name
154 | Ancient Brick Block
157 | Dirty Ancient Tile
160 | Ancient Golden Block
183 | Ancient Tile
185 | Mossy Ancient Tile
141 | Ancient Smooth Tile
200 | Starlight Block

[Hollow Halls Blocks]
ID  | Name
177 | Hollow Halls Block
197 | Obsidian Block
178 | Ectoplasm Block

[Luminescent Blocks]
ID  | Name
167 | Blue Luminescent Block
181 | Green Luminescent Block
182 | White Luminescent Block
198 | Yellow Luminescent Block

--]]