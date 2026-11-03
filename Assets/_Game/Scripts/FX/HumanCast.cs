using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>The climate a map's guards dress for.</summary>
    public enum CharacterBiome { Jungle, Desert, Snow }

    /// <summary>
    /// Who wears which body (the 0.7.0 cast): players are Rook (man or woman by seat, team colour on
    /// the jacket lining and armband), guards dress for the map's climate, bodyguards in dark suits,
    /// staff as waiter, cook, maid or mechanic, guests in summer clothes or the base outfits, targets as
    /// the tycoon or the magnate. The old MPFB bodies are no longer worn (Chr_Agent stays the clips' reference).
    /// Every choice is a pure function of networked data (role, seat, object id, map, spawn point),
    /// so every machine dresses an NPC the same way.
    /// </summary>
    public static class HumanCast
    {
        public const string AGENT = "Chr_Rook", AGENT_F = "Chr_Rook_F", BODYGUARD = "Chr_Bodyguard", STAFF = "Chr_Waiter";
        public const string CIVILIAN_M = "Chr_Guest_M", CIVILIAN_F = "Chr_Guest_F", VIP = "Chr_VIP_M", VIP_F = "Chr_VIP_F", ARMS = "Chr_Arms";
        private static readonly string[] STAFF_SET = { "Chr_Waiter", "Chr_Cook", "Chr_Maid", "Chr_Mechanic" };
        private static readonly string[] GUESTS_M = { "Chr_Guest_M", "Chr_Base_Slim", "Chr_Base_Average", "Chr_Base_Heavy" };
        private static readonly string[] GUESTS_F = { "Chr_Guest_F" };

        public static string ForAgent(int pSeat) => (pSeat & 1) == 0 ? AGENT : AGENT_F;

        public static string Guard(CharacterBiome pBiome) => pBiome switch
        {
            CharacterBiome.Desert => "Chr_Guard_Desert",
            CharacterBiome.Snow => "Chr_Guard_Snow",
            _ => "Chr_Guard_Jungle"
        };

        /// <summary>The map's climate from its names: snowy mountains, deserts and gulfs, jungles otherwise.</summary>
        public static CharacterBiome BiomeOf(MapDefinition pMap)
        {
            string lName = pMap != null ? (pMap.sceneName + " " + pMap.displayName).ToLowerInvariant() : "";
            if (lName.Contains("snow") || lName.Contains("weiss") || lName.Contains("alp") || lName.Contains("forest") || lName.Contains("winter") || lName.Contains("lodge")) return CharacterBiome.Snow;
            if (lName.Contains("desert") || lName.Contains("qasr") || lName.Contains("rimal") || lName.Contains("gulf") || lName.Contains("sand") || lName.Contains("oasis")) return CharacterBiome.Desert;
            return CharacterBiome.Jungle;
        }

        /// <summary>The body of an NPC.</summary>
        public static string ForNpc(NpcRole pRole, ulong pId, Vector3 pSpawn, MapDefinition pMap, TargetProfile pTarget)
        {
            switch (pRole)
            {
                case NpcRole.Target: return ForTarget(pTarget);
                case NpcRole.Bodyguard: return BODYGUARD;
                case NpcRole.Worker:
                {
                    // Staff where the staff pass is needed, guests where the invitation is; a mix elsewhere.
                    SecurityZone.At(pSpawn, out AccessLevel lAccess);
                    uint lRoll = Hash(pId);
                    if (lAccess == AccessLevel.Staff || (lAccess != AccessLevel.Guest && lRoll % 10 < 3)) return STAFF_SET[(lRoll / 10) % STAFF_SET.Length];
                    string[] lSet = (lRoll / 10 & 1) == 0 ? GUESTS_M : GUESTS_F;
                    return lSet[(lRoll / 20) % lSet.Length];
                }
                default: return Guard(BiomeOf(pMap));
            }
        }

        /// <summary>A target's body: its profile's choice when set, else a VIP (a woman's dress for women's names).</summary>
        public static string ForTarget(TargetProfile pTarget)
        {
            if (pTarget == null) return VIP;
            if (!string.IsNullOrEmpty(pTarget.character)) return pTarget.character;
            string lName = " " + pTarget.displayName.ToLowerInvariant() + " ";
            foreach (string lWord in FEMININE) if (lName.Contains(lWord)) return VIP_F;
            return VIP;
        }

        private static readonly string[] FEMININE =
        {
            " lady ", " sister ", " madame ", " mother", " mrs ", " miss ", " queen ", " duchess ",
            " marta ", " penny ", " ilse ", " noor ", " ziva ", " corvina ", " moss ", " ysolde ", " elena ", " sofia ", " maria ", " anna "
        };

        /// <summary>A small height variation per NPC (±3%).</summary>
        public static float ScaleFor(ulong pId) => 0.97f + (Hash(pId + 7) % 61) / 1000f;

        private static uint Hash(ulong pValue)
        {
            ulong x = pValue * 0x9E3779B97F4A7C15UL;
            x ^= x >> 31;
            return (uint)(x ^ (x >> 17));
        }
    }
}
