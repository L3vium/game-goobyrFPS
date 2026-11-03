using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>What a face shows. Local to each machine (worked out from the body's state), never networked.</summary>
    public enum FaceMood : byte { Neutral, Smile, Surprised, Angry, Scared, Pain, Dead }

    /// <summary>
    /// The cast's faces: the head mesh's blend shapes (Blink, Smile, Surprised, Angry, Scared, Pain,
    /// Dead) driven locally - a blink loop, a mood (<see cref="Mood"/>, set every frame by
    /// <see cref="HumanAnimator"/> from what the body does), short reactions on top (<see cref="React"/>:
    /// pain on a hit, surprise when spotting someone) and X eyes on death (<see cref="Die"/>: the eye
    /// material's base map is swapped). Cosmetic only, and idle while nobody sees the head.
    /// </summary>
    public class HumanFace : MonoBehaviour
    {
        private const string DEAD_EYES = "Characters/EyesDead";
        private static readonly string[] KEYS = { "Blink", "Smile", "Surprised", "Angry", "Scared", "Pain", "Dead" };
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static Texture2D _deadEyes;
        private static MaterialPropertyBlock _block;

        /// <summary>The standing expression (overridden for a moment by <see cref="React"/>).</summary>
        public FaceMood Mood { get; set; }
        public bool IsDead { get; private set; }

        private SkinnedMeshRenderer[] _renderers = new SkinnedMeshRenderer[0];
        private int[][] _index;               // per renderer, blend shape index of each KEYS entry (-1 = none)
        private float[] _weight = new float[KEYS.Length];
        private FaceMood _reaction;
        private float _reactionUntil;
        private float _nextBlink, _blinkStart = -1f;

        /// <summary>Finds the blend-shape heads under a model; false when the model has none (old cast).</summary>
        public bool Init(Transform pModel)
        {
            System.Collections.Generic.List<SkinnedMeshRenderer> lFound = new System.Collections.Generic.List<SkinnedMeshRenderer>();
            System.Collections.Generic.List<int[]> lIndex = new System.Collections.Generic.List<int[]>();
            foreach (SkinnedMeshRenderer lSkin in pModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh lMesh = lSkin.sharedMesh;
                if (lMesh == null || lMesh.blendShapeCount == 0) continue;
                int[] lMap = new int[KEYS.Length];
                bool lAny = false;
                for (int i = 0; i < KEYS.Length; i++) { lMap[i] = lMesh.GetBlendShapeIndex(KEYS[i]); lAny |= lMap[i] >= 0; }
                if (!lAny) continue;
                lFound.Add(lSkin);
                lIndex.Add(lMap);
            }
            _renderers = lFound.ToArray();
            _index = lIndex.ToArray();
            _nextBlink = Time.time + Random.Range(1f, 4f);
            return _renderers.Length > 0;
        }

        /// <summary>Shows <paramref name="pMood"/> for <paramref name="pSeconds"/> over the standing mood.</summary>
        public void React(FaceMood pMood, float pSeconds)
        {
            if (IsDead) return;
            _reaction = pMood;
            _reactionUntil = Time.time + pSeconds;
        }

        /// <summary>The slack dead face and X eyes on every LOD (the eye slot's base map swapped).</summary>
        public void Die()
        {
            if (IsDead) return;
            IsDead = true;
            if (_deadEyes == null) _deadEyes = Resources.Load<Texture2D>(DEAD_EYES);
            _block ??= new MaterialPropertyBlock();
            if (_deadEyes != null)
                foreach (Renderer lRenderer in GetComponentsInChildren<Renderer>(true))
                {
                    Material[] lMaterials = lRenderer.sharedMaterials;
                    for (int i = 0; i < lMaterials.Length; i++)
                    {
                        if (lMaterials[i] == null || !lMaterials[i].name.Contains("_Eyes")) continue;
                        lRenderer.GetPropertyBlock(_block, i);
                        _block.SetTexture(BaseMapId, _deadEyes);
                        lRenderer.SetPropertyBlock(_block, i);
                    }
                }
            for (int i = 0; i < KEYS.Length; i++) _weight[i] = KEYS[i] == "Dead" ? 100f : 0f;
            Apply();
        }

        private void LateUpdate()
        {
            if (IsDead || _renderers.Length == 0) return;
            bool lSeen = false;
            foreach (SkinnedMeshRenderer lSkin in _renderers) lSeen |= lSkin != null && lSkin.isVisible;
            if (!lSeen) return;

            FaceMood lMood = Time.time < _reactionUntil ? _reaction : Mood;
            float dt = Time.deltaTime;
            for (int i = 1; i < KEYS.Length; i++)
            {
                float lTarget = (int)lMood == i ? 100f : 0f;
                _weight[i] = Mathf.MoveTowards(_weight[i], lTarget, dt * (lTarget > _weight[i] ? 900f : 450f));
            }
            // Blink: a quick close and open every 2-5 s (none while the eyes are already screwed up in pain).
            if (_blinkStart < 0f && Time.time >= _nextBlink) _blinkStart = Time.time;
            float lBlink = 0f;
            if (_blinkStart >= 0f)
            {
                float t = (Time.time - _blinkStart) / 0.16f;
                lBlink = t < 0.45f ? t / 0.45f : Mathf.Clamp01(1f - (t - 0.45f) / 0.55f);
                if (t >= 1f) { _blinkStart = -1f; _nextBlink = Time.time + Random.Range(2f, 5f) * (lMood == FaceMood.Scared ? 0.5f : 1f); }
            }
            _weight[0] = lBlink * 100f * (1f - _weight[(int)FaceMood.Pain] / 100f);
            Apply();
        }

        private void Apply()
        {
            for (int r = 0; r < _renderers.Length; r++)
            {
                if (_renderers[r] == null) continue;
                for (int i = 0; i < KEYS.Length; i++)
                    if (_index[r][i] >= 0) _renderers[r].SetBlendShapeWeight(_index[r][i], _weight[i]);
            }
        }
    }
}
