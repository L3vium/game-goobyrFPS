using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    public partial class MapWeisshorn
    {
        /// <summary>
        /// The open snow round the Grand Hotel broken up: ploughed snowbanks, sledges, ski racks by the
        /// doors, a snowcat parked by the delivery road, a woodshed; luggage carts at the reception.
        /// </summary>
        private void Apron()
        {
            Rect lFront = Rect.MinMaxRect(HOTEL.x - 10f, HOTEL.z - 30f, HOTEL.x + 80f, HOTEL.z - 4f);
            Scatter(lFront, 9, 1.6f, 701, (g, y) => Snowbank(g, y));
            Scatter(lFront, 4, 1.0f, 702, (g, y) => Sledge(g, y));
            Scatter(lFront, 3, 1.5f, 703, (g, y) => SkiRack(g, Mathf.Round(y / 90f) * 90f));
            Scatter(lFront, 1, 3.2f, 704, (g, y) => Snowcat(g, y));
            Scatter(lFront, 2, 2.6f, 705, (g, y) => Shade(g, Mathf.Round(y / 90f) * 90f, 4f, 3f, 2.6f, ArchLib.Surface("grey_roof_tiles", 2f), ArchLib.Surface("dark_wood", 1.5f)));
            Scatter(Rect.MinMaxRect(HOTEL.x - 30f, HOTEL.z + 34f, HOTEL.x + 75f, HOTEL.z + 46f), 5, 1.6f, 706, (g, y) => Snowbank(g, y));
            // The reception: luggage carts by the desk.
            A.Model("Furn_Armchair", _hotelR.At(6f, 2, 3.2f, 0f), _hotelR.Yaw(160f), _hotelR.interior);
            Luggage(_hotelR.At(15.5f, 2, 3.4f, 0f), _hotelR.Yaw(20f));
            Luggage(_hotelR.At(5f, 2, 12.5f, 0f), _hotelR.Yaw(-15f));
        }
    }
}
