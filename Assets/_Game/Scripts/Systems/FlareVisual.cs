using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.AI;
using Com.IsartDigital.GoobyrFPS.Core;
using Com.IsartDigital.GoobyrFPS.Data;
using Com.IsartDigital.GoobyrFPS.FX;
using Com.IsartDigital.GoobyrFPS.Player;
using Com.IsartDigital.GoobyrFPS.Weapons;
using Unity.Netcode;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.Systems
{
    /// <summary>A red flare climbing into the sky, visible map-wide.</summary>
    public class FlareVisual : MonoBehaviour
    {
        private float _t;
        public static void Launch(Vector3 pFrom)
        {
            GameObject lFlare = GameArt.Part(null, "Flare", LowPolyMesh.Cached("w.sphere", () => LowPolyMesh.Icosphere(1)), pFrom, Vector3.one * 0.6f, new Color(3f, 0.4f, 0.3f), GameArt.Glow);
            Light lLight = lFlare.AddComponent<Light>();
            lLight.color = new Color(1f, 0.3f, 0.2f);
            lLight.range = 120f;
            lLight.intensity = 8f;
            lFlare.AddComponent<FlareVisual>();
            Object.Destroy(lFlare, 14f);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            transform.position += Vector3.up * Mathf.Max(0f, 30f - _t * 6f) * Time.deltaTime + new Vector3(Mathf.Sin(_t * 2f), 0, 0) * Time.deltaTime;
            if (Random.value < 0.4f) Effects.Dust(transform.position, 0.3f);
        }
    }
}
