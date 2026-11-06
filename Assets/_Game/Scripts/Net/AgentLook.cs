using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Net
{
    /// <summary>
    /// How a player dressed their agent and what they start with: indices into the tables below and into
    /// the game's items. It crosses the network with <see cref="LobbyPlayer"/>, and is kept between sessions.
    /// </summary>
    public struct AgentLook : INetworkSerializable, System.IEquatable<AgentLook>
    {
        public byte hat, skin, top, bottom, shoes;
        /// <summary>Item indices of the loadout; -1 (or anything out of range) for none.</summary>
        public short weapon, movement, distraction;

        public static readonly AgentLook Default = new AgentLook { hat = 0, skin = 0, top = 0, bottom = 0, shoes = 0, weapon = -1, movement = -1, distraction = -1 };

        public static readonly (string name, Color color)[] SKINS =
        {
            ("Peach", new Color(0.98f, 0.72f, 0.56f)), ("Tan", new Color(0.86f, 0.58f, 0.42f)), ("Brown", new Color(0.62f, 0.4f, 0.28f)), ("Pale", new Color(1f, 0.78f, 0.64f)),
            ("Lime", new Color(0.62f, 0.86f, 0.42f)), ("Lilac", new Color(0.72f, 0.6f, 0.92f)), ("Pumpkin", new Color(0.98f, 0.58f, 0.2f))
        };

        public static readonly (string name, Color color)[] CLOTHES =
        {
            ("Night suit", new Color(0.17f, 0.18f, 0.24f)), ("Snow", new Color(0.93f, 0.93f, 0.9f)), ("Coral", new Color(0.93f, 0.42f, 0.36f)), ("Mustard", new Color(0.93f, 0.72f, 0.24f)),
            ("Grass", new Color(0.42f, 0.68f, 0.32f)), ("Sky", new Color(0.42f, 0.68f, 0.9f)), ("Violet", new Color(0.52f, 0.36f, 0.72f)), ("Pink", new Color(0.95f, 0.6f, 0.74f)),
            ("Sand", new Color(0.82f, 0.7f, 0.5f)), ("Rust", new Color(0.62f, 0.32f, 0.2f)), ("Teal", new Color(0.2f, 0.6f, 0.6f)), ("Charcoal", new Color(0.3f, 0.3f, 0.32f))
        };

        public static readonly (string name, Color color)[] SHOES =
        {
            ("Black", new Color(0.15f, 0.13f, 0.13f)), ("White", new Color(0.92f, 0.92f, 0.9f)), ("Red", new Color(0.85f, 0.25f, 0.22f)), ("Yellow", new Color(0.95f, 0.78f, 0.24f)), ("Blue", new Color(0.3f, 0.5f, 0.85f)), ("Brown", new Color(0.42f, 0.27f, 0.16f))
        };

        /// <summary>Full outfits: hat, skin, top, bottom, shoes.</summary>
        public static readonly (string name, byte hat, byte skin, byte top, byte bottom, byte shoes)[] SUITS =
        {
            ("The Agent", 0, 0, 0, 0, 0), ("Ice Cream Man", 3, 3, 1, 1, 1), ("Holiday", 4, 1, 2, 8, 3), ("Beekeeper", 5, 0, 3, 11, 0),
            ("Gardener", 2, 4, 4, 9, 5), ("Clown Car", 1, 6, 7, 5, 2), ("Royal Mess", 6, 5, 6, 6, 3), ("Road Works", 7, 1, 3, 11, 0)
        };

        public static Color SkinColor(int pIndex) => SKINS[Mathf.Abs(pIndex) % SKINS.Length].color;
        public static Color TopColor(int pIndex) => CLOTHES[Mathf.Abs(pIndex) % CLOTHES.Length].color;
        public static Color BottomColor(int pIndex) => CLOTHES[Mathf.Abs(pIndex) % CLOTHES.Length].color;
        public static Color ShoesColor(int pIndex) => SHOES[Mathf.Abs(pIndex) % SHOES.Length].color;

        public void ApplySuit(int pSuit)
        {
            var lSuit = SUITS[Mathf.Abs(pSuit) % SUITS.Length];
            hat = lSuit.hat;
            skin = lSuit.skin;
            top = lSuit.top;
            bottom = lSuit.bottom;
            shoes = lSuit.shoes;
        }

        public void Randomize()
        {
            hat = (byte)Random.Range(0, FX.AgentHats.COUNT);
            skin = (byte)Random.Range(0, SKINS.Length);
            top = (byte)Random.Range(0, CLOTHES.Length);
            bottom = (byte)Random.Range(0, CLOTHES.Length);
            shoes = (byte)Random.Range(0, SHOES.Length);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> pSerializer) where T : IReaderWriter
        {
            pSerializer.SerializeValue(ref hat);
            pSerializer.SerializeValue(ref skin);
            pSerializer.SerializeValue(ref top);
            pSerializer.SerializeValue(ref bottom);
            pSerializer.SerializeValue(ref shoes);
            pSerializer.SerializeValue(ref weapon);
            pSerializer.SerializeValue(ref movement);
            pSerializer.SerializeValue(ref distraction);
        }

        public bool Equals(AgentLook pOther) => hat == pOther.hat && skin == pOther.skin && top == pOther.top && bottom == pOther.bottom && shoes == pOther.shoes
                                                && weapon == pOther.weapon && movement == pOther.movement && distraction == pOther.distraction;

        public override bool Equals(object pOther) => pOther is AgentLook lOther && Equals(lOther);
        public override int GetHashCode() => (hat, skin, top, bottom, shoes, weapon, movement, distraction).GetHashCode();
    }
}
