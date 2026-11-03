using System;
using System.Collections.Generic;
using Com.IsartDigital.GoobyrFPS.Core;
using UnityEngine;
using UnityEngine.Rendering;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// The sparkle of fresh snow: ice crystals scattered on the snowy ground around the camera (snow
    /// terrain layers and snow-covered meshes only), each one a tiny star that flashes when its facet
    /// mirrors the sun into the eye (<c>Goobyr/SnowGlint</c>), so the snow twinkles as you move.
    /// <para>
    /// The ground is sampled in 6 m tiles, a few per frame as the camera moves; a tile is the same
    /// every time it comes back (seeded by its coordinates). Added by <see cref="SnowFall"/>.
    /// </para>
    /// </summary>
    public class SnowGlints : MonoBehaviour
    {
        private const float TILE = 6f;
        private const int RADIUS_TILES = 5;
        private const int PER_TILE = 110;
        private const int TILES_PER_FRAME = 2;
        private const int BATCH = 1023;
        private const float SKY = 1500f;
        private const float SNOW_WEIGHT = 0.5f;

        private readonly Dictionary<Vector2Int, Matrix4x4[]> _tiles = new Dictionary<Vector2Int, Matrix4x4[]>();
        private readonly List<Vector2Int> _missing = new List<Vector2Int>();
        private readonly List<Vector2Int> _stale = new List<Vector2Int>();
        private readonly List<Matrix4x4> _buffer = new List<Matrix4x4>();
        private readonly Dictionary<Terrain, float[,]> _snowMaps = new Dictionary<Terrain, float[,]>();
        private readonly Dictionary<Collider, bool> _snowyColliders = new Dictionary<Collider, bool>();

        private Matrix4x4[] _instances = new Matrix4x4[0];
        private int _count;
        private bool _dirty;
        private Vector2Int _centre = new Vector2Int(int.MinValue, int.MinValue);
        private Material _material;
        private Mesh _quad;

        private void Awake()
        {
            _material = Resources.Load<Material>("Art/M_SnowGlint");
            _quad = BuildQuad();
        }

        private void OnDestroy()
        {
            if (_quad != null) Destroy(_quad);
        }

        private void LateUpdate()
        {
            Camera lCamera = Camera.main;
            if (lCamera == null || _material == null) return;
            Vector3 lEye = lCamera.transform.position;
            if (!World.GrassField.IsSane(lEye)) return;
            Vector2Int lCentre = new Vector2Int(Mathf.FloorToInt(lEye.x / TILE), Mathf.FloorToInt(lEye.z / TILE));
            if (lCentre != _centre)
            {
                _centre = lCentre;
                Refresh();
            }
            for (int i = 0; i < TILES_PER_FRAME && _missing.Count > 0; i++)
            {
                Vector2Int lTile = _missing[0];
                _missing.RemoveAt(0);
                _tiles[lTile] = Sample(lTile);
                _dirty = true;
            }
            if (_dirty) Gather();
            if (_count == 0) return;

            RenderParams lParams = new RenderParams(_material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(lEye, Vector3.one * (TILE * RADIUS_TILES * 2f + 20f))
            };
            for (int lStart = 0; lStart < _count; lStart += BATCH)
                Graphics.RenderMeshInstanced(lParams, _quad, 0, _instances, Mathf.Min(BATCH, _count - lStart), lStart);
        }

        /// <summary>Drops the tiles out of reach and queues the new ones, nearest first.</summary>
        private void Refresh()
        {
            _stale.Clear();
            foreach (Vector2Int lTile in _tiles.Keys) if (!InReach(lTile)) _stale.Add(lTile);
            foreach (Vector2Int lTile in _stale) _tiles.Remove(lTile);
            if (_stale.Count > 0) _dirty = true;

            _missing.Clear();
            for (int x = -RADIUS_TILES; x <= RADIUS_TILES; x++)
                for (int z = -RADIUS_TILES; z <= RADIUS_TILES; z++)
                {
                    Vector2Int lTile = new Vector2Int(_centre.x + x, _centre.y + z);
                    if (InReach(lTile) && !_tiles.ContainsKey(lTile)) _missing.Add(lTile);
                }
            _missing.Sort((a, b) => (a - _centre).sqrMagnitude.CompareTo((b - _centre).sqrMagnitude));
        }

        private bool InReach(Vector2Int pTile) => (pTile - _centre).sqrMagnitude <= RADIUS_TILES * RADIUS_TILES;

        /// <summary>The crystals of one tile: rays dropped from the sky, kept where they land on snow.</summary>
        private Matrix4x4[] Sample(Vector2Int pTile)
        {
            System.Random lRandom = new System.Random(pTile.x * 73856093 ^ pTile.y * 19349663);
            _buffer.Clear();
            for (int i = 0; i < PER_TILE; i++)
            {
                float x = (pTile.x + (float)lRandom.NextDouble()) * TILE;
                float z = (pTile.y + (float)lRandom.NextDouble()) * TILE;
                Vector3 lTilt = new Vector3((float)lRandom.NextDouble() - 0.5f, (float)lRandom.NextDouble() - 0.5f, (float)lRandom.NextDouble() - 0.5f) * 0.9f;
                if (!Physics.Raycast(new Vector3(x, SKY, z), Vector3.down, out RaycastHit lHit, SKY * 2f, GameLayers.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (lHit.normal.y < 0.55f || !IsSnow(lHit)) continue;
                Vector3 lFacet = (lHit.normal + lTilt).normalized;
                _buffer.Add(Matrix4x4.TRS(lHit.point + lHit.normal * 0.015f, Quaternion.FromToRotation(Vector3.up, lFacet), Vector3.one));
            }
            return _buffer.ToArray();
        }

        private void Gather()
        {
            _dirty = false;
            _count = 0;
            foreach (Matrix4x4[] lTile in _tiles.Values) _count += lTile.Length;
            if (_instances.Length < _count) _instances = new Matrix4x4[Mathf.NextPowerOfTwo(_count)];
            int lIndex = 0;
            foreach (Matrix4x4[] lTile in _tiles.Values)
            {
                Array.Copy(lTile, 0, _instances, lIndex, lTile.Length);
                lIndex += lTile.Length;
            }
        }

        /// <summary>Snow terrain (a snow layer above half weight) or a mesh wearing a snow material (snowy roofs, drifts).</summary>
        private bool IsSnow(RaycastHit pHit)
        {
            if (pHit.collider is TerrainCollider)
            {
                Terrain lTerrain = pHit.collider.GetComponent<Terrain>();
                return lTerrain != null && SnowAt(lTerrain, pHit.point) > SNOW_WEIGHT;
            }
            if (_snowyColliders.TryGetValue(pHit.collider, out bool lSnowy)) return lSnowy;
            Renderer lRenderer = pHit.collider.GetComponent<Renderer>();
            lSnowy = lRenderer != null && lRenderer.sharedMaterial != null && lRenderer.sharedMaterial.name.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0;
            _snowyColliders[pHit.collider] = lSnowy;
            return lSnowy;
        }

        private float SnowAt(Terrain pTerrain, Vector3 pPoint)
        {
            if (!_snowMaps.TryGetValue(pTerrain, out float[,] lSnow)) _snowMaps[pTerrain] = lSnow = SnowMap(pTerrain.terrainData);
            if (lSnow == null) return 0f;
            TerrainData lData = pTerrain.terrainData;
            Vector3 lLocal = pPoint - pTerrain.GetPosition();
            int lSize = lSnow.GetLength(0);
            int ix = Mathf.Clamp(Mathf.RoundToInt(lLocal.x / lData.size.x * (lSize - 1)), 0, lSize - 1);
            int iz = Mathf.Clamp(Mathf.RoundToInt(lLocal.z / lData.size.z * (lSize - 1)), 0, lSize - 1);
            return lSnow[iz, ix];
        }

        /// <summary>The summed weight of every snow layer of a terrain (null when it has none).</summary>
        private static float[,] SnowMap(TerrainData pData)
        {
            TerrainLayer[] lLayers = pData.terrainLayers;
            List<int> lSnowLayers = new List<int>();
            for (int i = 0; i < lLayers.Length; i++)
            {
                TerrainLayer lLayer = lLayers[i];
                if (lLayer == null) continue;
                bool lNamed = lLayer.name.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0;
                bool lTextured = lLayer.diffuseTexture != null && lLayer.diffuseTexture.name.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0;
                if (lNamed || lTextured) lSnowLayers.Add(i);
            }
            if (lSnowLayers.Count == 0) return null;
            int lSize = pData.alphamapResolution;
            float[,,] lAlpha = pData.GetAlphamaps(0, 0, lSize, lSize);
            float[,] lSnow = new float[lSize, lSize];
            for (int z = 0; z < lSize; z++)
                for (int x = 0; x < lSize; x++)
                {
                    float lWeight = 0f;
                    foreach (int lLayer in lSnowLayers) lWeight += lAlpha[z, x, lLayer];
                    lSnow[z, x] = lWeight;
                }
            return lSnow;
        }

        private static Mesh BuildQuad()
        {
            Mesh lMesh = new Mesh { name = "SnowGlintQuad" };
            lMesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) });
            lMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            lMesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            lMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return lMesh;
        }
    }
}
