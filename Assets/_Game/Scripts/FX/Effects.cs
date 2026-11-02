using System.Collections.Generic;
using UnityEngine;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.GoobyrFPS.FX
{
    /// <summary>
    /// Every one-shot visual effect, built from code-made particle systems and pooled: muzzle
    /// flashes, tracers, impacts, fluff puffs on hits, explosions (fireball, shockwave, debris,
    /// light flash), smoke clouds, fire, gas, water spray and sparks. Local on each machine.
    /// </summary>
    public static class Effects
    {
        /// <summary>Colour of the spray a bullet or a blow throws out of a body.</summary>
        public static readonly Color Blood = new Color(0.42f, 0.03f, 0.03f);

        private static readonly Dictionary<string, Queue<ParticleSystem>> _pools = new Dictionary<string, Queue<ParticleSystem>>();
        private static Transform _root;

        private static Transform Root
        {
            get
            {
                if (_root == null) _root = new GameObject("EffectsPool").transform;
                return _root;
            }
        }

        // ---------------------------------------------------------------- public effects

        public static void Muzzle(Vector3 pPosition, Vector3 pDirection, float pSize = 1f)
        {
            Emit("muzzle", pPosition, Quaternion.LookRotation(pDirection), Mathf.RoundToInt(6 * pSize), pSize);
            FlashLight(pPosition + pDirection * 0.3f, new Color(1f, 0.8f, 0.45f), 6f * pSize, 5f, 0.05f);
        }

        public static void Tracer(Vector3 pFrom, Vector3 pTo, Color pColor)
        {
            GameObject lTracer = new GameObject("Tracer");
            LineRenderer lLine = lTracer.AddComponent<LineRenderer>();
            lLine.sharedMaterial = GameArt.Glow;
            lLine.positionCount = 2;
            lLine.SetPosition(0, pFrom);
            lLine.SetPosition(1, pTo);
            lLine.widthMultiplier = 0.045f;
            lLine.startColor = pColor;
            lLine.endColor = new Color(pColor.r, pColor.g, pColor.b, 0.2f);
            lLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lTracer.AddComponent<FadeLine>().Init(0.07f);
            // A shot crossing water throws up a column where it meets the surface.
            Vector3 lPath = pTo - pFrom;
            if (Systems.WaterVolume.Raycast(pFrom, lPath.normalized, lPath.magnitude, out Vector3 lWater)) Splash(lWater, 0.5f);
        }

        public static void Impact(Vector3 pPosition, Vector3 pNormal, Color pColor)
        {
            Emit("impact", pPosition, Quaternion.LookRotation(pNormal), 8, 1f, pColor);
            Emit("sparks", pPosition, Quaternion.LookRotation(pNormal), 5, 1f);
        }

        /// <summary>Hit on a character: a spray of particles in the given colour (blood for bodies).</summary>
        public static void Fluff(Vector3 pPosition, Vector3 pDirection, Color pColor, bool pBig)
        {
            Emit("fluff", pPosition, Quaternion.LookRotation(pDirection), pBig ? 22 : 10, pBig ? 1.4f : 1f, pColor);
        }

        public static void Explosion(Vector3 pPosition, float pRadius)
        {
            float lScale = Mathf.Clamp(pRadius / 5f, 0.5f, 2.5f);
            Emit("fireball", pPosition, Quaternion.identity, Mathf.RoundToInt(26 * lScale), lScale);
            Emit("embers", pPosition, Quaternion.identity, Mathf.RoundToInt(40 * lScale), lScale);
            Emit("debris", pPosition, Quaternion.identity, Mathf.RoundToInt(18 * lScale), lScale);
            Emit("smoke", pPosition + Vector3.up, Quaternion.identity, Mathf.RoundToInt(14 * lScale), lScale * 1.4f);
            Emit("flashcore", pPosition, Quaternion.identity, 1, lScale * 2.2f);
            Shockwave(pPosition, pRadius * 1.6f);
            FlashLight(pPosition + Vector3.up, new Color(1f, 0.6f, 0.25f), 40f * lScale, pRadius * 3f, 0.4f);
        }

        public static void SmokeCloud(Vector3 pPosition, float pRadius, float pDuration)
        {
            ParticleSystem lSmoke = Build("smokecloud");
            lSmoke.transform.position = pPosition;
            ParticleSystem.MainModule lMain = lSmoke.main;
            lMain.duration = pDuration;
            ParticleSystem.ShapeModule lShape = lSmoke.shape;
            lShape.radius = pRadius * 0.6f;
            lSmoke.Play();
            Object.Destroy(lSmoke.gameObject, pDuration + 8f);
        }

        /// <summary>A looping fire on a burning object; destroy the returned object to put it out.</summary>
        public static GameObject Fire(Transform pParent, Vector3 pLocal, float pSize)
        {
            ParticleSystem lFire = Build("fireloop");
            lFire.transform.SetParent(pParent, false);
            lFire.transform.localPosition = pLocal;
            lFire.transform.localScale = Vector3.one * pSize;
            lFire.Play();
            Light lLight = new GameObject("FireLight").AddComponent<Light>();
            lLight.transform.SetParent(lFire.transform, false);
            lLight.transform.localPosition = Vector3.up * 0.8f;
            lLight.color = new Color(1f, 0.55f, 0.2f);
            lLight.range = 7f * pSize;
            lLight.intensity = 4f;
            lLight.gameObject.AddComponent<Flicker>();
            return lFire.gameObject;
        }

        public static GameObject Gas(Transform pParent, Vector3 pLocal)
        {
            ParticleSystem lGas = Build("gasloop");
            lGas.transform.SetParent(pParent, false);
            lGas.transform.localPosition = pLocal;
            lGas.Play();
            return lGas.gameObject;
        }

        public static GameObject Spray(Transform pParent, Vector3 pLocal)
        {
            ParticleSystem lSpray = Build("sprayloop");
            lSpray.transform.SetParent(pParent, false);
            lSpray.transform.localPosition = pLocal;
            lSpray.transform.localRotation = Quaternion.Euler(90, 0, 0);
            lSpray.Play();
            return lSpray.gameObject;
        }

        public static void Sparks(Vector3 pPosition, int pCount = 12) => Emit("sparks", pPosition, Quaternion.LookRotation(Vector3.up), pCount, 1.3f);

        public static void Dust(Vector3 pPosition, float pScale = 1f) => Emit("dust", pPosition, Quaternion.identity, Mathf.RoundToInt(10 * pScale), pScale);

        /// <summary>Water thrown up (a body, a dive, a bullet); 1 = a person falling in.</summary>
        public static void Splash(Vector3 pPosition, float pSize = 1f)
        {
            Emit("splash", pPosition, Quaternion.LookRotation(Vector3.up), Mathf.RoundToInt(16 * pSize) + 4, Mathf.Max(0.4f, pSize));
            Audio.Sfx.Play(pSize > 0.7f ? "splash" : "splash_small", pPosition, Mathf.Clamp01(0.4f + pSize * 0.6f), 30f);
        }

        public static void Puke(Vector3 pPosition, Vector3 pDirection) => Emit("puke", pPosition, Quaternion.LookRotation(pDirection), 14, 1f);

        public static void Confetti(Vector3 pPosition) => Emit("confetti", pPosition, Quaternion.LookRotation(Vector3.up), 60, 1.2f);

        public static void FlashLight(Vector3 pPosition, Color pColor, float pIntensity, float pRange, float pDuration)
        {
            Light lLight = new GameObject("FlashLight").AddComponent<Light>();
            lLight.transform.position = pPosition;
            lLight.type = LightType.Point;
            lLight.color = pColor;
            lLight.intensity = pIntensity;
            lLight.range = pRange;
            lLight.shadows = LightShadows.None;
            lLight.gameObject.AddComponent<FadeLight>().Init(pDuration);
        }

        private static void Shockwave(Vector3 pPosition, float pRadius)
        {
            GameObject lRing = new GameObject("Shockwave");
            lRing.transform.position = pPosition + Vector3.up * 0.15f;
            lRing.AddComponent<MeshFilter>().sharedMesh = LowPolyMesh.Cached("fx.ring", () => LowPolyMesh.Cylinder(24, 1f));
            MeshRenderer lRenderer = lRing.AddComponent<MeshRenderer>();
            lRenderer.sharedMaterial = GameArt.Glow;
            lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lRing.AddComponent<GrowFade>().Init(pRadius, 0.35f, new Color(1f, 0.85f, 0.6f));
        }

        // ---------------------------------------------------------------- pool

        private static void Emit(string pKind, Vector3 pPosition, Quaternion pRotation, int pCount, float pScale, Color? pTint = null)
        {
            if (!_pools.TryGetValue(pKind, out Queue<ParticleSystem> lPool)) { lPool = new Queue<ParticleSystem>(); _pools[pKind] = lPool; }
            ParticleSystem lSystem = null;
            while (lPool.Count > 0 && lSystem == null) lSystem = lPool.Dequeue();
            if (lSystem == null) lSystem = Build(pKind);
            lPool.Enqueue(lSystem);

            lSystem.transform.SetPositionAndRotation(pPosition, pRotation);
            lSystem.transform.localScale = Vector3.one * pScale;
            ParticleSystem.EmitParams lParams = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            if (pTint.HasValue) lParams.startColor = pTint.Value;
            lSystem.Emit(lParams, pCount);
        }

        private static ParticleSystem Build(string pKind)
        {
            GameObject lObject = new GameObject("FX_" + pKind);
            lObject.transform.SetParent(Root);
            ParticleSystem lSystem = lObject.AddComponent<ParticleSystem>();
            lSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer lRenderer = lObject.GetComponent<ParticleSystemRenderer>();
            lRenderer.sharedMaterial = GameArt.ParticleAdd;
            lRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            lRenderer.mesh = LowPolyMesh.Cached("fx.blob", () => LowPolyMesh.Icosphere(0));
            lRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ParticleSystem.MainModule lMain = lSystem.main;
            lMain.playOnAwake = false;
            lMain.loop = false;
            lMain.scalingMode = ParticleSystemScalingMode.Hierarchy;
            lMain.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule lEmission = lSystem.emission;
            lEmission.rateOverTime = 0f;
            ParticleSystem.ShapeModule lShape = lSystem.shape;
            ParticleSystem.ColorOverLifetimeModule lColor = lSystem.colorOverLifetime;
            ParticleSystem.SizeOverLifetimeModule lSize = lSystem.sizeOverLifetime;

            switch (pKind)
            {
                case "muzzle":
                    Setup(lMain, 0.06f, 0.09f, 6f, 14f, 0.12f, 0.28f, new Color(1f, 0.85f, 0.4f), 0f);
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 18f; lShape.radius = 0.02f;
                    FadeShrink(lColor, lSize);
                    break;
                case "impact":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 0.3f, 0.6f, 1.5f, 4f, 0.06f, 0.16f, new Color(0.8f, 0.75f, 0.65f), 1.5f);
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 35f; lShape.radius = 0.05f;
                    FadeShrink(lColor, lSize);
                    break;
                case "sparks":
                    Setup(lMain, 0.15f, 0.35f, 4f, 10f, 0.02f, 0.05f, new Color(1f, 0.8f, 0.35f), 2.5f);
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 50f; lShape.radius = 0.02f;
                    lRenderer.renderMode = ParticleSystemRenderMode.Stretch; lRenderer.velocityScale = 0.06f; lRenderer.lengthScale = 1f;
                    lRenderer.mesh = null;
                    FadeShrink(lColor, lSize);
                    break;
                case "fluff":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 0.5f, 1f, 2f, 5f, 0.07f, 0.17f, Color.white, 1.2f);
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 40f; lShape.radius = 0.08f;
                    lMain.startRotation3D = true;
                    FadeShrink(lColor, lSize);
                    break;
                case "fireball":
                    Setup(lMain, 0.45f, 0.9f, 3f, 9f, 0.9f, 2f, new Color(1f, 0.65f, 0.2f), -0.4f);
                    lShape.shapeType = ParticleSystemShapeType.Sphere; lShape.radius = 0.8f;
                    Gradient(lColor, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.45f, 0.1f), new Color(0.3f, 0.1f, 0.05f));
                    lSize.enabled = true; lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.4f));
                    break;
                case "flashcore":
                    Setup(lMain, 0.12f, 0.14f, 0f, 0f, 3f, 3f, new Color(1f, 0.95f, 0.85f), 0f);
                    lShape.enabled = false;
                    FadeShrink(lColor, lSize);
                    break;
                case "embers":
                    Setup(lMain, 0.6f, 1.6f, 6f, 18f, 0.04f, 0.1f, new Color(1f, 0.7f, 0.3f), 2f);
                    lShape.shapeType = ParticleSystemShapeType.Sphere; lShape.radius = 0.5f;
                    lRenderer.renderMode = ParticleSystemRenderMode.Stretch; lRenderer.velocityScale = 0.05f; lRenderer.mesh = null;
                    FadeShrink(lColor, lSize);
                    break;
                case "debris":
                    lRenderer.sharedMaterial = GameArt.Toon;
                    lRenderer.mesh = LowPolyMesh.Cached("fx.cube", () => LowPolyMesh.Box());
                    Setup(lMain, 1.2f, 2.2f, 6f, 15f, 0.12f, 0.35f, new Color(0.35f, 0.3f, 0.28f), 2.2f);
                    lMain.startRotation3D = true;
                    lShape.shapeType = ParticleSystemShapeType.Hemisphere; lShape.radius = 0.6f;
                    ParticleSystem.CollisionModule lCollision = lSystem.collision;
                    lCollision.enabled = true; lCollision.type = ParticleSystemCollisionType.World; lCollision.bounce = 0.3f; lCollision.dampen = 0.4f;
                    ParticleSystem.RotationOverLifetimeModule lRot = lSystem.rotationOverLifetime; lRot.enabled = true; lRot.separateAxes = true;
                    lRot.x = new ParticleSystem.MinMaxCurve(-6f, 6f); lRot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
                    lSize.enabled = true; lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.6f));
                    break;
                case "smoke":
                case "dust":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, pKind == "smoke" ? 2.5f : 1.2f, pKind == "smoke" ? 4.5f : 2f, 0.5f, 2.5f, 0.8f, 1.8f, pKind == "smoke" ? new Color(0.35f, 0.33f, 0.36f, 0.8f) : new Color(0.8f, 0.75f, 0.65f, 0.6f), -0.08f);
                    lShape.shapeType = ParticleSystemShapeType.Sphere; lShape.radius = 0.7f;
                    lColor.enabled = true;
                    Gradient(lColor, lMain.startColor.color, lMain.startColor.color, new Color(1, 1, 1, 0));
                    lSize.enabled = true; lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.6f, 1, 1.8f));
                    break;
                case "smokecloud":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 4f, 6f, 0.2f, 1f, 2.2f, 3.6f, new Color(0.88f, 0.88f, 0.9f, 0.95f), -0.02f);
                    lMain.loop = false;
                    lEmission.rateOverTime = 26f;
                    lShape.shapeType = ParticleSystemShapeType.Sphere; lShape.radius = 3f;
                    lColor.enabled = true;
                    Gradient(lColor, new Color(1, 1, 1, 0), Color.white, new Color(1, 1, 1, 0));
                    lSize.enabled = true; lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.4f));
                    break;
                case "fireloop":
                    Setup(lMain, 0.5f, 0.9f, 1f, 2.5f, 0.35f, 0.7f, new Color(1f, 0.6f, 0.15f), -0.6f);
                    lMain.loop = true;
                    lEmission.rateOverTime = 30f;
                    lShape.shapeType = ParticleSystemShapeType.Sphere; lShape.radius = 0.4f;
                    Gradient(lColor, new Color(1f, 0.95f, 0.6f), new Color(1f, 0.4f, 0.1f), new Color(0.2f, 0.05f, 0.02f, 0f));
                    lSize.enabled = true; lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1f, 1, 0.1f));
                    break;
                case "gasloop":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 3f, 5f, 0.5f, 1.5f, 1f, 2.2f, new Color(0.55f, 0.95f, 0.35f, 0.5f), -0.03f);
                    lMain.loop = true;
                    lEmission.rateOverTime = 10f;
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 30f; lShape.radius = 0.3f;
                    lColor.enabled = true;
                    Gradient(lColor, new Color(0.5f, 1f, 0.3f, 0f), new Color(0.5f, 1f, 0.3f, 0.6f), new Color(0.5f, 1f, 0.3f, 0f));
                    lSize.enabled = true; lSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 2.2f));
                    break;
                case "sprayloop":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 0.6f, 1f, 4f, 7f, 0.04f, 0.08f, new Color(0.7f, 0.85f, 1f, 0.8f), 1.5f);
                    lMain.loop = true;
                    lEmission.rateOverTime = 60f;
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 55f; lShape.radius = 0.05f;
                    FadeShrink(lColor, lSize);
                    break;
                case "splash":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 0.5f, 0.9f, 3f, 6f, 0.08f, 0.18f, new Color(0.75f, 0.9f, 1f, 0.9f), 2f);
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 25f; lShape.radius = 0.2f;
                    FadeShrink(lColor, lSize);
                    break;
                case "puke":
                    lRenderer.sharedMaterial = GameArt.ParticleAlpha;
                    Setup(lMain, 0.6f, 1f, 2f, 4f, 0.06f, 0.12f, new Color(0.6f, 0.85f, 0.25f, 1f), 1.8f);
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 15f; lShape.radius = 0.03f;
                    FadeShrink(lColor, lSize);
                    break;
                case "confetti":
                    lRenderer.sharedMaterial = GameArt.Toon;
                    lRenderer.mesh = LowPolyMesh.Cached("fx.cube", () => LowPolyMesh.Box());
                    Setup(lMain, 2f, 3.5f, 6f, 12f, 0.06f, 0.12f, Color.white, 0.6f);
                    lMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0.5f), new Color(0.4f, 0.8f, 1f));
                    lMain.startRotation3D = true;
                    lShape.shapeType = ParticleSystemShapeType.Cone; lShape.angle = 35f;
                    break;
            }
            return lSystem;
        }

        private static void Setup(ParticleSystem.MainModule pMain, float pLifeMin, float pLifeMax, float pSpeedMin, float pSpeedMax, float pSizeMin, float pSizeMax, Color pColor, float pGravity)
        {
            pMain.startLifetime = new ParticleSystem.MinMaxCurve(pLifeMin, pLifeMax);
            pMain.startSpeed = new ParticleSystem.MinMaxCurve(pSpeedMin, pSpeedMax);
            pMain.startSize = new ParticleSystem.MinMaxCurve(pSizeMin, pSizeMax);
            pMain.startColor = pColor;
            pMain.gravityModifier = pGravity;
            pMain.maxParticles = 500;
        }

        private static void FadeShrink(ParticleSystem.ColorOverLifetimeModule pColor, ParticleSystem.SizeOverLifetimeModule pSize)
        {
            pSize.enabled = true;
            pSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
        }

        private static void Gradient(ParticleSystem.ColorOverLifetimeModule pModule, Color pA, Color pB, Color pC)
        {
            pModule.enabled = true;
            Gradient lGradient = new Gradient();
            lGradient.SetKeys(
                new[] { new GradientColorKey(pA, 0f), new GradientColorKey(pB, 0.4f), new GradientColorKey(pC, 1f) },
                new[] { new GradientAlphaKey(pA.a, 0f), new GradientAlphaKey(pB.a, 0.4f), new GradientAlphaKey(pC.a, 1f) });
            pModule.color = lGradient;
        }
    }

    public class FadeLine : MonoBehaviour
    {
        private LineRenderer _line;
        private float _time, _duration;
        public void Init(float pDuration) { _line = GetComponent<LineRenderer>(); _duration = pDuration; }
        private void Update()
        {
            _time += Time.deltaTime;
            _line.widthMultiplier = 0.045f * (1f - _time / _duration);
            if (_time >= _duration) Destroy(gameObject);
        }
    }

    public class FadeLight : MonoBehaviour
    {
        private Light _light;
        private float _start, _time, _duration;
        public void Init(float pDuration) { _light = GetComponent<Light>(); _start = _light.intensity; _duration = pDuration; }
        private void Update()
        {
            _time += Time.deltaTime;
            _light.intensity = _start * (1f - _time / _duration);
            if (_time >= _duration) Destroy(gameObject);
        }
    }

    public class GrowFade : MonoBehaviour
    {
        private float _radius, _duration, _time;
        private Renderer _renderer;
        private Color _color;
        public void Init(float pRadius, float pDuration, Color pColor) { _radius = pRadius; _duration = pDuration; _color = pColor; _renderer = GetComponent<Renderer>(); }
        private void Update()
        {
            _time += Time.deltaTime;
            float t = _time / _duration;
            float lSize = Mathf.Lerp(0.5f, _radius * 2f, 1f - (1f - t) * (1f - t));
            transform.localScale = new Vector3(lSize, 0.05f, lSize);
            GameArt.Paint(_renderer, _color * (1f - t) * 3f);
            if (_time >= _duration) Destroy(gameObject);
        }
    }

    public class Flicker : MonoBehaviour
    {
        private Light _light;
        private float _base, _seed;
        private void Awake() { _light = GetComponent<Light>(); _base = _light.intensity; _seed = Random.value * 10f; }
        private void Update() => _light.intensity = _base * (0.75f + Mathf.PerlinNoise(_seed, Time.time * 8f) * 0.5f);
    }
}
