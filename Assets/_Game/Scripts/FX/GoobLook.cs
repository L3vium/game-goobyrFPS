using Com.IsartDigital.GoobyrFPS.Data;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    public enum Outfit { Suit, Guard, Military, Casual, Worker, Fancy, Biker, Chef, Doctor }
    public enum Hair { None, Short, Bun, Long, Mohawk }
    public enum Facial { None, Beard, Mustache }
    public enum HandStyle { Mitten, Glove }
    public enum FootStyle { Shoe, Boot, Sneaker }

    /// <summary>
    /// Everything that makes one Goob look like itself: outfit, head, hair, hat, hands, feet,
    /// accessories and colours. The parts are the Blender-modelled pieces in <c>Resources/Goob</c>.
    /// </summary>
    [System.Serializable]
    public struct GoobLook
    {
        public Outfit outfit;
        public int head;
        public Hair hair;
        public Facial facial;
        public HatStyle hat;
        public bool helmet, chefHat, headset, sunglasses, earpiece;
        public HandStyle hands;
        public FootStyle feet;
        public Color clothes, skin, hairColor, hatColor;
        public float scale;

        public static GoobLook Default(Color pClothes) => new GoobLook
        {
            outfit = Outfit.Suit, head = 0, hair = Hair.Short, hat = HatStyle.None, hands = HandStyle.Mitten, feet = FootStyle.Shoe,
            clothes = pClothes, skin = new Color(1f, 0.83f, 0.7f), hairColor = new Color(0.25f, 0.18f, 0.12f), hatColor = pClothes * 0.6f, scale = 1f
        };

        /// <summary>A varied civilian-ish look drawn from a seed (skins, hair, heads).</summary>
        public static GoobLook Random(System.Random pRandom, Outfit pOutfit, Color pClothes)
        {
            Color[] lSkins = { new Color(1f, 0.85f, 0.72f), new Color(0.95f, 0.75f, 0.6f), new Color(0.8f, 0.58f, 0.42f), new Color(0.6f, 0.42f, 0.3f), new Color(0.45f, 0.3f, 0.22f) };
            Color[] lHairs = { new Color(0.12f, 0.1f, 0.08f), new Color(0.35f, 0.22f, 0.12f), new Color(0.75f, 0.55f, 0.3f), new Color(0.55f, 0.2f, 0.1f), new Color(0.8f, 0.8f, 0.78f) };
            GoobLook lLook = Default(pClothes);
            lLook.outfit = pOutfit;
            lLook.head = pRandom.Next(3);
            lLook.hair = (Hair)pRandom.Next(5);
            lLook.facial = pRandom.NextDouble() < 0.25 ? (Facial)(1 + pRandom.Next(2)) : Facial.None;
            lLook.skin = lSkins[pRandom.Next(lSkins.Length)];
            lLook.hairColor = lHairs[pRandom.Next(lHairs.Length)];
            lLook.feet = pOutfit == Outfit.Military || pOutfit == Outfit.Guard || pOutfit == Outfit.Biker || pOutfit == Outfit.Worker ? FootStyle.Boot : pOutfit == Outfit.Casual ? FootStyle.Sneaker : FootStyle.Shoe;
            lLook.scale = 0.95f + (float)pRandom.NextDouble() * 0.12f;
            return lLook;
        }
    }
}
