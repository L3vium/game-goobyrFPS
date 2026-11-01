using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>The building families of the realistic maps, as PBR material sets.</summary>
    public static class ArchStyles
    {
        /// <summary>Colombian colonial: white lime, terracotta tiles, stone trim, blue-green shutters.</summary>
        public static ArchStyle Colonial(Color pFacade, string pSuffix, Color? pShutter = null)
        {
            Color lShutter = pShutter ?? new Color(0.22f, 0.5f, 0.5f);
            return new ArchStyle
            {
                facade = ArchLib.Tinted("painted_plaster_wall", pFacade, "col_" + pSuffix, 3.5f),
                interior = ArchLib.Tinted("painted_plaster_wall", new Color(0.96f, 0.94f, 0.88f), "col_inside", 3f),
                trim = ArchLib.Tinted("large_sandstone_blocks", new Color(0.9f, 0.88f, 0.84f), "col_trim", 2f),
                plinth = ArchLib.Surface("stone_wall", 2.5f),
                roof = ArchLib.Surface("clay_roof_tiles_02", 2.4f),
                flatRoof = ArchLib.Tinted("large_floor_tiles_02", new Color(0.86f, 0.55f, 0.42f), "terracotta", 2f),
                shutter = ArchLib.Tinted("old_planks_02", lShutter, "shutter_" + ColorUtility.ToHtmlStringRGB(lShutter), 1.2f),
                frame = ArchLib.Tinted("wooden_planks", new Color(0.92f, 0.92f, 0.88f), "frame_white", 1f),
                door = ArchLib.Surface("dark_wooden_planks", 1.5f),
                ceiling = ArchLib.Tinted("painted_plaster_wall", new Color(1f, 0.99f, 0.96f), "ceiling", 3f),
                stair = ArchLib.Tinted("large_sandstone_blocks", new Color(0.9f, 0.88f, 0.84f), "col_trim", 2f),
                column = ArchLib.Tinted("painted_plaster_wall", new Color(1f, 0.99f, 0.96f), "ceiling", 3f),
                soffit = ArchLib.Surface("wooden_planks", 2f),
                cellarWall = ArchLib.Surface("stone_wall", 2.5f),
                floorDefault = ArchLib.Tinted("large_floor_tiles_02", new Color(0.86f, 0.55f, 0.42f), "terracotta", 2f),
                lightColor = new Color(1f, 0.8f, 0.58f),
                outerThickness = 0.5f,
                windowSpacing = 3.4f
            };
        }

        /// <summary>A plain working building: concrete block, corrugated roof, metal doors.</summary>
        public static ArchStyle Industrial(string pWall = "concrete_wall_008", string pRoof = "corrugated_iron_02")
        {
            return new ArchStyle
            {
                facade = ArchLib.Surface(pWall, 3f),
                interior = ArchLib.Surface(pWall, 3f),
                trim = ArchLib.Surface("concrete_floor_02", 2f),
                plinth = ArchLib.Surface("concrete_floor_02", 2f),
                roof = ArchLib.Surface(pRoof, 2.5f),
                flatRoof = ArchLib.Surface("concrete_floor_02", 3f),
                shutter = null,
                frame = ArchLib.Surface("metal_plate", 1f),
                door = ArchLib.Surface("painted_metal_shutter", 1.5f),
                ceiling = ArchLib.Surface(pWall, 3f),
                stair = ArchLib.Surface("concrete_floor_02", 2f),
                column = ArchLib.Surface("metal_plate", 1.5f),
                soffit = ArchLib.Surface("metal_plate", 2f),
                cellarWall = ArchLib.Surface("concrete_wall_008", 3f),
                floorDefault = ArchLib.Surface("concrete_floor_02", 3f),
                lightColor = new Color(0.95f, 0.95f, 0.9f),
                shutters = false,
                outerThickness = 0.3f,
                windowSpacing = 4.5f
            };
        }

        /// <summary>A timber hut: planks inside and out, tin or tile roof.</summary>
        public static ArchStyle Timber(string pRoof = "corrugated_iron_02")
        {
            return new ArchStyle
            {
                facade = ArchLib.Surface("old_planks_02", 2f),
                interior = ArchLib.Surface("wooden_planks", 2f),
                trim = ArchLib.Surface("rough_wood", 1.5f),
                plinth = ArchLib.Surface("stone_wall", 2.5f),
                roof = ArchLib.Surface(pRoof, 2.5f),
                flatRoof = ArchLib.Surface("old_planks_02", 2f),
                shutter = ArchLib.Surface("rough_wood", 1.2f),
                frame = ArchLib.Surface("rough_wood", 1f),
                door = ArchLib.Surface("old_planks_02", 1.5f),
                ceiling = ArchLib.Surface("wooden_planks", 2f),
                stair = ArchLib.Surface("old_planks_02", 1.5f),
                column = ArchLib.Surface("rough_wood", 1.5f),
                soffit = ArchLib.Surface("wooden_planks", 2f),
                cellarWall = ArchLib.Surface("stone_wall", 2.5f),
                floorDefault = ArchLib.Surface("old_planks_02", 2f),
                lightColor = new Color(1f, 0.78f, 0.55f),
                outerThickness = 0.25f,
                windowSpacing = 3f
            };
        }
            /// <summary>Desert adobe: ochre clay plaster, sandstone trim, flat roofs, blue doors, dark wood screens.</summary>
        public static ArchStyle Adobe(Color pFacade, string pSuffix, bool pBlueDoor = true)
        {
            return new ArchStyle
            {
                facade = ArchLib.Tinted("clay_plaster", pFacade, "adobe_" + pSuffix, 3.5f),
                interior = ArchLib.Tinted("clay_plaster", new Color(0.98f, 0.94f, 0.86f), "adobe_inside", 3f),
                trim = ArchLib.Tinted("large_sandstone_blocks", new Color(0.95f, 0.86f, 0.72f), "sand_trim", 2f),
                plinth = ArchLib.Surface("large_sandstone_blocks", 2.5f),
                roof = ArchLib.Tinted("clay_plaster", new Color(0.86f, 0.74f, 0.58f), "adobe_roof", 3f),
                flatRoof = ArchLib.Tinted("clay_plaster", new Color(0.86f, 0.74f, 0.58f), "adobe_roof", 3f),
                shutter = ArchLib.Tinted("wooden_planks", new Color(0.35f, 0.24f, 0.16f), "screen", 1.2f),
                frame = ArchLib.Tinted("wooden_planks", new Color(0.4f, 0.28f, 0.18f), "frame_brown", 1f),
                door = pBlueDoor ? ArchLib.Tinted("old_planks_02", new Color(0.18f, 0.35f, 0.7f), "door_blue", 1.5f) : ArchLib.Surface("dark_wooden_planks", 1.5f),
                ceiling = ArchLib.Tinted("clay_plaster", new Color(0.98f, 0.94f, 0.86f), "adobe_inside", 3f),
                stair = ArchLib.Tinted("large_sandstone_blocks", new Color(0.95f, 0.86f, 0.72f), "sand_trim", 2f),
                column = ArchLib.Tinted("large_sandstone_blocks", new Color(0.95f, 0.86f, 0.72f), "sand_trim", 2f),
                soffit = ArchLib.Surface("dark_wooden_planks", 2f),
                cellarWall = ArchLib.Surface("large_sandstone_blocks", 2.5f),
                floorDefault = ArchLib.Surface("large_floor_tiles_02", 2f),
                lightColor = new Color(1f, 0.78f, 0.52f),
                outerThickness = 0.55f,
                windowSpacing = 4.2f
            };
        }

        /// <summary>Fortress masonry (the keep): sandstone blocks outside, plaster inside.</summary>
        public static ArchStyle Fortress()
        {
            ArchStyle s = Adobe(new Color(0.92f, 0.8f, 0.62f), "keep", false);
            s.facade = ArchLib.Surface("large_sandstone_blocks", 2.6f);
            s.outerThickness = 0.8f;
            s.windowSpacing = 4.6f;
            return s;
        }

        /// <summary>Alpine: dark timber over a stone base, steep slate roofs, snow on them.</summary>
        public static ArchStyle Alpine(bool pTimber = true)
        {
            return new ArchStyle
            {
                facade = pTimber ? ArchLib.Surface("dark_wooden_planks", 2f) : ArchLib.Surface("stone_wall", 2.5f),
                interior = ArchLib.Surface("wooden_planks", 2f),
                trim = ArchLib.Tinted("stone_wall", new Color(0.85f, 0.85f, 0.85f), "alp_trim", 2f),
                plinth = ArchLib.Surface("stone_wall", 2.5f),
                roof = ArchLib.Surface("grey_roof_tiles", 2.4f),
                flatRoof = ArchLib.Surface("concrete_floor_02", 3f),
                shutter = ArchLib.Tinted("wooden_planks", new Color(0.55f, 0.12f, 0.1f), "alp_shutter", 1.2f),
                frame = ArchLib.Tinted("wooden_planks", new Color(0.92f, 0.9f, 0.86f), "frame_white", 1f),
                door = ArchLib.Surface("dark_wood", 1.5f),
                ceiling = ArchLib.Surface("wooden_planks", 2f),
                stair = ArchLib.Surface("dark_wooden_planks", 1.5f),
                column = ArchLib.Surface("dark_wood", 1.5f),
                soffit = ArchLib.Surface("dark_wooden_planks", 2f),
                cellarWall = ArchLib.Surface("stone_wall", 2.5f),
                floorDefault = ArchLib.Surface("wood_floor_deck", 2f),
                lightColor = new Color(1f, 0.75f, 0.45f),
                outerThickness = 0.45f,
                windowSpacing = 3.2f,
                snow = true
            };
        }
    }
}
