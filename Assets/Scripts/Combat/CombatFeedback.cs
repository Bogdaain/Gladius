using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// The juice layer. Listens to this object's <see cref="Health"/> and turns
    /// each blow into feedback: a blood burst at the hit point, a camera-shake
    /// punch, and a brief hitstop. Blood sparks differently for clean hits,
    /// blocks, and parries so each reads at a glance.
    ///
    /// Everything is driven off Health.Hit, so it never touches the damage math.
    /// The blood ParticleSystem is built in code, so no prefab or asset setup is
    /// needed; assign one in the inspector later if you want custom art.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CombatFeedback : MonoBehaviour
    {
        [Header("Blood prefabs (leave empty to use the code-generated fallback)")]
        [Tooltip("Spawned on a clean hit. If empty, a burst is generated in code.")]
        [SerializeField] private ParticleSystem bloodPrefab;
        [Tooltip("Optional heavier burst for heavy hits. Falls back to bloodPrefab, then code.")]
        [SerializeField] private ParticleSystem heavyBloodPrefab;
        [SerializeField] private Color bloodColor = new Color(0.55f, 0.02f, 0.02f);
        [SerializeField] private int lightBloodCount = 18;
        [SerializeField] private int heavyBloodCount = 40;

        [Header("Spark prefabs (leave empty to use the code-generated fallback)")]
        [Tooltip("Spawned on a block. If empty, sparks are generated in code.")]
        [SerializeField] private ParticleSystem blockSparkPrefab;
        [Tooltip("Spawned on a parry. If empty, sparks are generated in code.")]
        [SerializeField] private ParticleSystem parrySparkPrefab;
        [Tooltip("Spark colour for a blocked hit, used only by the code fallback.")]
        [SerializeField] private Color blockSparkColor = new Color(1f, 0.85f, 0.4f);
        [Tooltip("Spark colour for a parry, used only by the code fallback.")]
        [SerializeField] private Color parrySparkColor = new Color(0.6f, 0.9f, 1f);
        [Tooltip("Seconds before a spawned prefab instance is destroyed.")]
        [SerializeField] private float prefabLifetime = 3f;

        [Header("Camera shake (only fires if this character has a PlayerController)")]
        [SerializeField, Range(0f, 1f)] private float lightHitTrauma = 0.25f;
        [SerializeField, Range(0f, 1f)] private float heavyHitTrauma = 0.5f;
        [SerializeField, Range(0f, 1f)] private float blockTrauma = 0.15f;
        [SerializeField, Range(0f, 1f)] private float parryTrauma = 0.4f;

        [Header("Hitstop (seconds)")]
        [SerializeField] private float lightHitstop = 0.07f;
        [SerializeField] private float heavyHitstop = 0.13f;
        [SerializeField] private float blockHitstop = 0.05f;
        [SerializeField] private float parryHitstop = 0.18f;
        [Tooltip("A clean hit's freeze stretches with damage dealt: full bonus at this damage. " +
                 "Lets a charged heavy pause noticeably longer than a light tap. 0 = off.")]
        [SerializeField] private float hitstopDamageReference = 40f;
        [Tooltip("Extra freeze added at the damage reference, on top of the base hitstop.")]
        [SerializeField] private float hitstopDamageBonus = 0.08f;

        private Health health;
        private PlayerController player;       // null on enemies; used for shake
        private ParticleSystem bloodInstance;  // code-built burst, reused
        private ParticleSystem sparkInstance;  // code-built sparks for block/parry
        private Texture2D dotTexture;          // shared soft-dot sprite

        private void Awake()
        {
            health = GetComponent<Health>();
            player = GetComponentInParent<PlayerController>();

            // Only build the code-generated fallbacks for slots that have no prefab.
            // With prefabs assigned for everything, nothing is generated.
            if (bloodPrefab == null) bloodInstance = BuildBloodSystem();
            if (blockSparkPrefab == null || parrySparkPrefab == null) sparkInstance = BuildSparkSystem();
        }

        private void OnEnable() => health.Hit += OnHit;
        private void OnDisable() => health.Hit -= OnHit;

        private void OnHit(HitInfo hit, HitResult result)
        {
            switch (result)
            {
                case HitResult.Hit:
                    bool heavy = hit.Type == AttackType.Heavy;
                    SpawnBlood(hit.Point, hit.Direction, heavy);
                    Shake(heavy ? heavyHitTrauma : lightHitTrauma);

                    // Base freeze by attack type, stretched by how much damage landed
                    // so a charged heavy pauses longer than a light tap.
                    float baseStop = heavy ? heavyHitstop : lightHitstop;
                    float bonus = hitstopDamageReference > 0f
                        ? hitstopDamageBonus * Mathf.Clamp01(hit.Damage / hitstopDamageReference)
                        : 0f;
                    Hitstop.Do(baseStop + bonus);
                    break;

                case HitResult.Blocked:
                    SpawnSparks(hit.Point, hit.Direction, blockSparkPrefab, 20, blockSparkColor);
                    Shake(blockTrauma);
                    Hitstop.Do(blockHitstop);
                    break;

                case HitResult.Parried:
                    SpawnSparks(hit.Point, hit.Direction, parrySparkPrefab, 35, parrySparkColor);
                    Shake(parryTrauma);
                    Hitstop.Do(parryHitstop);
                    break;
            }
        }

        private void Shake(float trauma)
        {
            if (player != null) player.AddTrauma(trauma);
        }

        private void SpawnBlood(Vector3 point, Vector3 direction, bool heavy)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up;
            Quaternion rot = Quaternion.LookRotation(direction.normalized);

            // Prefer an authored prefab (heavy variant if given), else code fallback.
            ParticleSystem prefab = heavy && heavyBloodPrefab != null ? heavyBloodPrefab : bloodPrefab;
            if (prefab != null)
            {
                var ps = Instantiate(prefab, point, rot);
                ps.Play();
                Destroy(ps.gameObject, prefabLifetime);
                return;
            }

            bloodInstance.transform.SetPositionAndRotation(point, rot);
            var main = bloodInstance.main;
            main.startColor = bloodColor;
            bloodInstance.Emit(heavy ? heavyBloodCount : lightBloodCount);
        }

        /// <summary>
        /// Metal sparks for blocks and parries: fast, thin, bright streaks that
        /// fly out and die quickly, with no gravity so they read as sparks rather
        /// than droplets. Distinct motion from blood so each outcome looks different.
        /// </summary>
        private void SpawnSparks(Vector3 point, Vector3 direction, ParticleSystem prefab, int count, Color color)
        {
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up;
            // Sparks fly back toward the attacker (opposite the blow) plus scatter.
            Quaternion rot = Quaternion.LookRotation(-direction.normalized);

            if (prefab != null)
            {
                var ps = Instantiate(prefab, point, rot);
                ps.Play();
                Destroy(ps.gameObject, prefabLifetime);
                return;
            }

            sparkInstance.transform.SetPositionAndRotation(point, rot);
            var main = sparkInstance.main;
            main.startColor = color;
            sparkInstance.Emit(count);
        }

        // ───────────── Generated particle systems ─────────────

        /// <summary>Blood: round droplets that spray along the blow, fall, and fade.</summary>
        private ParticleSystem BuildBloodSystem()
        {
            var go = new GameObject("BloodBurst (generated)");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            main.gravityModifier = 1.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.startColor = bloodColor;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.05f;

            // Shrink and fade as they die, so droplets taper off rather than pop out.
            FadeAndShrink(ps, 0.6f);
            ApplyRoundMaterial(ps, false);
            return ps;
        }

        /// <summary>Sparks: fast thin streaks, no gravity, brief life, additive glow.</summary>
        private ParticleSystem BuildSparkSystem()
        {
            var go = new GameObject("SparkBurst (generated)");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.gravityModifier = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 40f; // wider scatter than blood
            shape.radius = 0.03f;

            // Stretch sparks along their velocity so they read as streaks.
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 2.5f;

            FadeAndShrink(ps, 0.4f);
            ApplyRoundMaterial(ps, true);
            return ps;
        }

        /// <summary>Alpha fades to zero and size shrinks over the back of each life.</summary>
        private static void FadeAndShrink(ParticleSystem ps, float fadeStart)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
        }

        /// <summary>
        /// Puts a generated soft round dot on the particles so they aren't flat
        /// squares. Additive for sparks (glow), alpha-blended for blood.
        /// </summary>
        private void ApplyRoundMaterial(ParticleSystem ps, bool additive)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            var shaderName = additive
                ? "Universal Render Pipeline/Particles/Unlit"
                : "Universal Render Pipeline/Particles/Unlit";
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            if (dotTexture == null) dotTexture = BuildSoftDot();
            mat.mainTexture = dotTexture;
            if (additive && mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f); // additive if supported
            renderer.material = mat;
        }

        /// <summary>A small radial-gradient dot texture: opaque centre, soft edge.</summary>
        private static Texture2D BuildSoftDot()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a; // sharper falloff = rounder, softer edge
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        private void OnDestroy()
        {
            if (bloodInstance != null) Destroy(bloodInstance.gameObject);
            if (sparkInstance != null) Destroy(sparkInstance.gameObject);
            if (dotTexture != null) Destroy(dotTexture);
        }
    }
}
