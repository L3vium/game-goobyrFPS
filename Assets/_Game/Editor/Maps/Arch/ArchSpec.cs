using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Systems;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>What a room is for: its floor, its furniture, its light, its windows.</summary>
    public enum RoomType
    {
        Hall, Salon, Dining, Kitchen, Pantry, Office, Study, Bedroom, Suite, Bath, Chapel, Sacristy,
        Corridor, Gallery, Stair, Store, Cellar, WineCellar, ColdRoom, CoalStore, Laundry, Security, Vault,
        GuardRoom, Linen, Dormitory, Archive, Garage, Workshop, Bar, Shop, Church, Police, Hangar,
        Radio, Mess, Barracks, Lobby, Spa, Gym, Terrace, Loggia, Attic, Belvedere, Crypt, Living, Classroom,
        Plant, Armoury, Library, Ballroom, Clinic
    }

    public enum DoorKind { Doorway, Door, Double, Secure, Hatch }

    public enum WindowKind { Glazed, Open, French, Small, Boarded, Shuttered, Slit }

    public enum RoofKind { Gable, Hip, Shed, Flat }

    /// <summary>
    /// A room of a building: a rectangle on a floor (building-local metres, multiples of 0.5),
    /// what it is and its security tag. Rooms sharing a name are one space (no wall between them).
    /// </summary>
    public class ArchRoom
    {
        public string name;
        public int floor;
        public Rect rect;
        public RoomType type;
        public SecurityTier tier = SecurityTier.Restricted;
        public AccessLevel access;
        /// <summary>Its sides to the outside (and to voids) are an arcade: columns, arches, a balustrade upstairs.</summary>
        public bool arcade;
        /// <summary>A terrace or a roof walk: a floor and a parapet, no walls of its own.</summary>
        public bool open;
        public bool noWindows;
        public bool noFurniture;
        public bool noLight;
        public bool noZone;
        /// <summary>The upper part of a double-height space (a nave, a hangar): no slab under it, nothing in it.</summary>
        public bool noFloor;
        /// <summary>Wider auto windows (a glass salon): 0 keeps the default.</summary>
        public float windowWidth;
        public ArchMat floorMat;
        public ArchMat wallMat;
        public int index;

        public Vector2 Center => rect.center;
        public bool Enclosed => !open && !arcade && !isVoid;
        /// <summary>The upper part of a double-height hall: facades to the outside, open to its galleries, no floor.</summary>
        public bool isVoid;
        /// <summary>An indoor gallery: toward a void it gets a balustrade, not a wall (atrium corridors, a mezzanine).</summary>
        public bool overlook;
    }

    public class ArchDoor
    {
        public int floor;
        public Vector2 at;
        public float width = 1.2f;
        public float height = 2.35f;
        public DoorKind kind = DoorKind.Door;
        public SecureDoor.Lock lockKind;
        /// <summary>NPCs walk through it (no NavMesh obstacle while shut): household doors on routes.</summary>
        public bool npcPass;
        /// <summary>Bottom of the opening (a boarded window, a hatch in a wall).</summary>
        public float sill;
        public string poi;
    }

    public class ArchWindow
    {
        public int floor;
        public Vector2 at;
        public float width = 1.2f;
        public WindowKind kind = WindowKind.Glazed;
        public bool balcony;
        public string poi;
    }

    /// <summary>A flight of stairs filling a rectangle from one floor to the next.</summary>
    public class ArchStair
    {
        public Rect rect;
        public int fromFloor;
        /// <summary>The side the climb starts from: 0 south (climbs north), 1 east, 2 north, 3 west.</summary>
        public int startSide;
        /// <summary>Two flights and a landing (U-turn) instead of one straight run.</summary>
        public bool dogleg = true;
        /// <summary>Dogleg: the first flight runs in the left half (seen climbing).</summary>
        public bool leftFirst = true;
        public ArchMat mat;
    }

    public class ArchRoof
    {
        public Rect rect;
        /// <summary>The roof sits on top of this floor's walls.</summary>
        public int onFloor;
        public RoofKind kind = RoofKind.Hip;
        public bool ridgeAlongX = true;
        public float pitch = 26f;
        public float overhang = 0.6f;
        /// <summary>Shed roofs: the low side (0 south, 1 east, 2 north, 3 west).</summary>
        public int lowSide;
        public ArchMat mat;
    }

    /// <summary>
    /// A crawlable vent: a chain of 1 m cells (axis-aligned legs between nodes, building-local, y =
    /// the duct floor). Grilles close the given ends; vertical runs get a climbable side.
    /// </summary>
    public class ArchDuct
    {
        public List<Vector3> nodes = new List<Vector3>();
        /// <summary>Grilles: a node and the direction its face opens to.</summary>
        public List<(int node, Vector3Int dir, string poi)> grilles = new List<(int, Vector3Int, string)>();
    }

    public class ArchBalcony
    {
        public int floor;
        public Vector2 at;
        public float width = 2.4f;
        public float depth = 1.1f;
    }

    /// <summary>A drainpipe (climbable) from the ground to the eaves at a point of the outer wall.</summary>
    public class ArchDrain
    {
        public Vector2 at;
        public int topFloor = -1;
        public string poi;
    }

    /// <summary>Materials of a building family (stucco casa, adobe, chalet...).</summary>
    public class ArchStyle
    {
        public ArchMat facade, interior, trim, plinth, roof, flatRoof, shutter, frame, door, ceiling, stair, column, floorDefault, cellarWall, cellarFloor, soffit;
        public Color lightColor = new Color(1f, 0.82f, 0.62f);
        public bool shutters = true;
        public float windowSpacing = 3.4f;
        /// <summary>Heavy masonry: thicker outer walls, deeper reveals.</summary>
        public float outerThickness = 0.45f;
        public bool snow;
    }

    /// <summary>A building to generate: footprint, floors, rooms, openings, stairs, roofs, vents, climbs.</summary>
    public class ArchSpec
    {
        public string name = "Building";
        public Vector3 origin;
        public float yaw;
        public Vector2 size = new Vector2(10, 8);
        public float storey = 3.6f;
        public float slab = 0.3f;
        public ArchStyle style;
        public int zone;
        public readonly List<ArchRoom> rooms = new List<ArchRoom>();
        public readonly List<ArchDoor> doors = new List<ArchDoor>();
        public readonly List<ArchWindow> windows = new List<ArchWindow>();
        public readonly List<ArchStair> stairs = new List<ArchStair>();
        public readonly List<ArchRoof> roofs = new List<ArchRoof>();
        public readonly List<ArchDuct> ducts = new List<ArchDuct>();
        public readonly List<ArchBalcony> balconies = new List<ArchBalcony>();
        public readonly List<ArchDrain> drains = new List<ArchDrain>();
        /// <summary>Extra holes in a floor slab (chutes, hatches): floor and rectangle.</summary>
        public readonly List<(int floor, Rect rect)> holes = new List<(int, Rect)>();
        /// <summary>Floors that get a stone cornice ledge (0.35 m deep, hangable) along the facades.</summary>
        public readonly List<int> cornices = new List<int>();
        /// <summary>Auto windows on the facades (off for shut-up houses).</summary>
        public bool autoWindows = true;
        public bool furnish = true;
        public bool lights = true;
        public bool zones = true;
        /// <summary>Plinth depth below the ground floor (hides a sloping pad).</summary>
        public float plinth = 1.6f;
        /// <summary>Cut the land over the whole footprint (a building dug into a slope).</summary>
        public bool cutLand;
        /// <summary>A house nobody enters: solid, shutters closed, no interior.</summary>
        public bool shut;

        /// <summary>A room two (or more) storeys high: the same rectangle repeated above with no slab.</summary>
        public ArchRoom Tall(string pName, int pFloor, int pStoreys, float pX0, float pZ0, float pX1, float pZ1, RoomType pType, SecurityTier pTier = SecurityTier.Restricted, AccessLevel pAccess = AccessLevel.None)
        {
            ArchRoom lRoom = Room(pName, pFloor, pX0, pZ0, pX1, pZ1, pType, pTier, pAccess);
            for (int f = 1; f < pStoreys; f++)
            {
                ArchRoom lUpper = Room(pName, pFloor + f, pX0, pZ0, pX1, pZ1, pType, pTier, pAccess);
                lUpper.noFloor = lUpper.noFurniture = lUpper.noLight = lUpper.noZone = true;
            }
            return lRoom;
        }

        public ArchRoom Room(string pName, int pFloor, float pX0, float pZ0, float pX1, float pZ1, RoomType pType, SecurityTier pTier = SecurityTier.Restricted, AccessLevel pAccess = AccessLevel.None)
        {
            ArchRoom lRoom = new ArchRoom { name = pName, floor = pFloor, rect = Rect.MinMaxRect(pX0, pZ0, pX1, pZ1), type = pType, tier = pTier, access = pAccess, index = rooms.Count };
            rooms.Add(lRoom);
            return lRoom;
        }

        public ArchDoor Door(int pFloor, float pX, float pZ, DoorKind pKind = DoorKind.Door, float pWidth = 1.2f)
        {
            ArchDoor lDoor = new ArchDoor { floor = pFloor, at = new Vector2(pX, pZ), kind = pKind, width = pWidth };
            doors.Add(lDoor);
            return lDoor;
        }

        public ArchDoor Secure(int pFloor, float pX, float pZ, SecureDoor.Lock pLock, bool pNpcPass = false, string pPoi = null)
        {
            ArchDoor lDoor = new ArchDoor { floor = pFloor, at = new Vector2(pX, pZ), kind = DoorKind.Secure, lockKind = pLock, npcPass = pNpcPass, poi = pPoi };
            doors.Add(lDoor);
            return lDoor;
        }

        public ArchWindow Window(int pFloor, float pX, float pZ, WindowKind pKind, float pWidth = 1.2f)
        {
            ArchWindow lWindow = new ArchWindow { floor = pFloor, at = new Vector2(pX, pZ), kind = pKind, width = pWidth };
            windows.Add(lWindow);
            return lWindow;
        }

        public ArchStair Stair(int pFrom, float pX0, float pZ0, float pX1, float pZ1, int pStartSide, bool pDogleg = true, bool pLeftFirst = true)
        {
            ArchStair lStair = new ArchStair { fromFloor = pFrom, rect = Rect.MinMaxRect(pX0, pZ0, pX1, pZ1), startSide = pStartSide, dogleg = pDogleg, leftFirst = pLeftFirst };
            stairs.Add(lStair);
            return lStair;
        }

        public ArchRoof Roof(int pOnFloor, float pX0, float pZ0, float pX1, float pZ1, RoofKind pKind, bool pAlongX = true, float pPitch = 26f)
        {
            ArchRoof lRoof = new ArchRoof { onFloor = pOnFloor, rect = Rect.MinMaxRect(pX0, pZ0, pX1, pZ1), kind = pKind, ridgeAlongX = pAlongX, pitch = pPitch };
            roofs.Add(lRoof);
            return lRoof;
        }

        public int MinFloor
        {
            get { int m = 0; foreach (ArchRoom r in rooms) m = Mathf.Min(m, r.floor); return m; }
        }

        public int MaxFloor
        {
            get { int m = 0; foreach (ArchRoom r in rooms) m = Mathf.Max(m, r.floor); return m; }
        }
    }
}
