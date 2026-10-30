using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Data;
using UnityEditor;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.EditorTools
{
    /// <summary>
    /// A small street that shows one building of each kit style, props and Goobs: the place to
    /// check the kit's orientation, roofs, stairs and outfits after touching the assembler.
    /// Saved as <c>Scenes/KitTest.unity</c> (not in the build).
    /// </summary>
    public static class KitTestScene
    {
        [MenuItem("Goobyr/Build/Kit Test Scene")]
        public static void Build()
        {
            MapDefinition lMap = ScriptableObject.CreateInstance<MapDefinition>();
            MapDefinition lSource = GameDatabase.Instance.maps[1];
            EditorUtility.CopySerialized(lSource, lMap);
            lMap.sceneName = "KitTest";
            MapKit K = new MapKit(lMap, 5);
            K.Grid(30, 30, 4f, new Vector2(-60, -60), new Color(0.62f, 0.72f, 0.45f));
            K.Paint(new Rect(-60, -6, 120, 12), new Color(0.42f, 0.42f, 0.45f));

            string[] lStyles = { "Timber", "Stucco", "Suburb", "Classic" };
            for (int i = 0; i < lStyles.Length; i++)
            {
                MapKit.KitSpec lSpec = new MapKit.KitSpec
                {
                    name = lStyles[i], style = lStyles[i], origin = new Vector3(-42 + i * 26, 0, 16), width = 5, depth = 4, floors = 2 + i % 2,
                    roof = i == 2 ? MapKit.Roof.Gable : i == 0 ? MapKit.Roof.Hip : MapKit.Roof.Flat, tallWindows = i == 3, brick = i == 2 && false, balconyChance = 0.2f,
                    wall = new[] { Color.white, new Color(0.98f, 0.9f, 0.78f), new Color(0.75f, 0.85f, 0.95f), new Color(0.95f, 0.93f, 0.88f) }[i],
                    climb = new Vector2Int(1, 1), seed = 10 + i
                };
                lSpec.doors.Add(new Vector2Int(0, 2));
                lSpec.arches.Add(new Vector2Int(0, 0));
                lSpec.rooms.Add(new[] { MapKit.Room.Bar, MapKit.Room.Lounge });
                lSpec.rooms.Add(new[] { MapKit.Room.Office, MapKit.Room.Bedroom });
                lSpec.rooms.Add(new[] { MapKit.Room.Dining });
                K.Building(lSpec);
            }

            string[] lProps = { "Bench", "StreetLamp", "TrashBin", "Hydrant", "MarketStall", "Parasol", "Sedan", "PoliceCar", "ShippingContainer", "Fountain", "Dumpster", "Barrel" };
            for (int i = 0; i < lProps.Length; i++) K.Prop(lProps[i], new Vector3(-45 + i * 8, 0, -10), 0, null, MapKit.Collide.Box);
            string[] lNature = { "PineB", "BroadleafA", "PalmA", "BirchA", "CypressA", "BushB", "RockC", "HedgeBlock" };
            for (int i = 0; i < lNature.Length; i++) K.NaturePiece(lNature[i], new Vector3(-45 + i * 12, 0, -26), 0, 1f);
            K.Point(AI.MatchPoint.Kind.Start, new Vector3(0, 0.2f, 0), 0, "Test");
            K.Finish();

            // Goobs lined up to check the outfits.
            int lIndex = 0;
            foreach (FX.Outfit lOutfit in System.Enum.GetValues(typeof(FX.Outfit)))
            {
                GameObject lGoob = new GameObject("Goob_" + lOutfit);
                lGoob.transform.position = new Vector3(-12 + lIndex * 3f, 0, 2);
                lGoob.transform.rotation = Quaternion.Euler(0, 180, 0);
                FX.GoobBody lBody = lGoob.AddComponent<FX.GoobBody>();
                FX.GoobLook lLook = FX.GoobLook.Random(new System.Random(lIndex * 31), lOutfit, Net.LobbyPlayer.TeamColor(lIndex));
                lLook.hat = (HatStyle)(lIndex % 9);
                lBody.Configure(lLook);
                lBody.Build();
                lIndex++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log("[Build] kit test scene");
        }
    }
}
