-- ========================================
--          Terrain Replacer
-- ========================================

-- Enables swapping the placement of terrain materials
Enable_VoxelMaterialReplacer = false

-- Enabled | Replace Material GUID | With Material ID
-- ex. { enabled = true,  targetGuid = "8870fa83-04e6-4b5a-ac2a-ea13205ce469", toId = 34 }, -- Replace Stone with Lava
VoxelMaterialReplacements = {
    { enabled = true,  targetGuid = "20292e7b-858b-4e5f-bdc8-e921190eb0f4", toId = 34 }, -- Springlands Dirt | Lava
    { enabled = false, targetGuid = "ca261385-9aa6-4fa4-94c5-cd28f0565669", toId = 16 },  -- Revelwood Dirt | Red Shroud Essence
    { enabled = false, targetGuid = "e37da6be-626f-4232-846c-80be26a5adfb", toId = 49 },  -- Nomad Highlands Dirt | Toxic Slime
    { enabled = false, targetGuid = "95652556-6b4d-46b4-a6ce-9a4b997b792d", toId = 94 },  -- Veilwater Basin Dirt | Shroud Slime
    { enabled = false, targetGuid = "323c469c-004b-4a49-89a4-f204d2100b04", toId = 71 },  -- Albaneve Summits Dirt | Acidic Mycelium
    { enabled = false, targetGuid = "", toId = nil },
    { enabled = false, targetGuid = "", toId = nil },
}

-- ----------------------------------------
--       Terrain Reference Table
-- ----------------------------------------

--[[

FULL TERRAIN ID LIST:
https://docs.google.com/spreadsheets/d/1hMTF2QMDnfnwD2lbhJtHRPj-awAU1-DUuethcbzkZB4/edit?usp=sharing

Note: Useful IDs
[Dirt]
ID | Name                  | GUID
 1 | Springlands Dirt      | 20292e7b-858b-4e5f-bdc8-e921190eb0f4
12 | Revelwood Dirt        | ca261385-9aa6-4fa4-94c5-cd28f0565669
11 | Nomad Highlands Dirt  | e37da6be-626f-4232-846c-80be26a5adfb
10 | Veilwater Basin Dirt  | 95652556-6b4d-46b4-a6ce-9a4b997b792d
58 | Albaneve Summits Dirt | 323c469c-004b-4a49-89a4-f204d2100b04

[Misc]
ID | Name           | GUID
35 | Twig Nesting   | 8c2b7ff9-52b3-4b03-8e33-df22d3e40c90
60 | Raw Red Marble | f0fd3814-504c-419e-bdcc-b26553e13f1d
 5 | Bedrock        | 00000000-0000-0000-0000-000000000000
 7 | Obsidian       | c6283555-6ee3-4e53-91ac-280c316968a5

[Deposits]
ID | Name       | GUID
19 | Flintstone | 91d3f063-2bcb-4a35-b257-5b930df563db
25 | Clay       | d8fe3777-ae68-408c-b242-bee90a915731
17 | Salt       | e62ddb3c-9c6f-4cc7-86db-2ba78065912d
18 | Amber      | 25e6f60d-44a2-4a39-82d0-92391a2a995f
30 | Sulfur Ore | 99bb8b93-9959-474f-8d9c-64c10cb1973d
 6 | Coal       | 75475b1e-e17c-449c-8aef-28df15ae5e76

[Ore]
ID | Name       | GUID
26 | Copper Ore | c29a5ecb-1c7a-4dd5-84fc-2fff8cdf0ada
27 | Tin Ore    | 767a4256-c082-4a6e-a6e5-129a44058063
29 | Iron Ore   | 9a58bec9-ed97-4730-a7e6-a6cd5eec0af0
63 | Silver Ore | 29cfa47f-d501-4aa9-8895-2bff58d3eee9
89 | Gold Ore   | 68807927-26ec-4791-b6d5-6d09198159be

[Gems]
ID | Name            | GUID
24 | Amethyst        | d8fe3777-ae68-408c-b242-bee90a915731
97 | Mother of Pearl | 1e182425-a89f-4e93-bb17-489b11ff9d29
31 | Lapis Lazuli    | a3888566-0bea-4a9d-911d-cbc039d1a56b
96 | Aquamarine      | a433ccca-7d3c-45d5-9767-5c2a217e1bbc

[Hazards]
ID | Name | GUID
37 | Mud  | 20292e7b-858b-4e5f-bdc8-e921190eb0f4
73 | Mud  | 20292e7b-858b-4e5f-bdc8-e921190eb0f4
51 | Tar  | 1af1de41-7ad5-4484-8cde-0dc07f63bed6
34 | Lava | 00000000-0000-0000-0000-000000000000

[Bone]
ID | Name            | GUID
38 | Fossilized Bone | 36f61aae-10e8-4d21-bc27-31342f5f621b
48 | Bone Pile       | af6a8201-7081-4178-8097-4c24ad976769
50 | Ecto Bone Pile  | 271fcfca-920d-451e-81dd-adb5d07479b0

[Shroud]
ID | Name                  | GUID
33 | Glowing Mycelium      | 7022db55-cbfe-4bff-9804-9a727be0798d
72 | Mycelium (Green Vein) | 689fce88-018b-4b0f-9b28-2cc478e0c0e6
71 | Acidic Mycelium       | d4812564-c69d-4e92-b830-7d2511ef0274
76 | Shroud Sand           | b3b235b7-2a0a-44b4-94b4-2d1abe909f2b
49 | Toxic Slime           | 6448e357-93fd-472b-8998-9953c4ae7d36
94 | Shroud Slime          | a4b9ebf3-7600-49dc-bffc-3d1d6e8548fd
16 | Red Shroud Essence    | 00000000-0000-0000-0000-000000000000

[Archaic Essence]
ID | Name                       | GUID
68 | Archaic Essence lvl 5      | ab3b4edf-b63f-4db4-a7ec-751141766a5a
82 | Archaic Essence lvl 13     | 2371f5e7-7c90-47b4-b0f8-1643d4f6d1b4
83 | Archaic Essence lvl 18     | 7e001767-b15a-4c50-b686-8662b14a7b39
84 | Archaic Essence lvl 25     | aafd9cc0-78a2-45c1-97f9-ad3b12f9abc9
85 | Archaic Essence lvl 33     | 535c9a3c-e2a1-43fe-8884-fedbd48565f6
98 | Archaic Essence lvl 33 (2) | 8b204e6f-ac80-4bb5-9a82-76a9bd2f28ed

--]]

