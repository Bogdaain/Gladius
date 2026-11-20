using System.Collections.Generic;
using Gladius.Combat;
using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// A fast flinch when this enemy is struck: a brief colour blink on its
    /// renderers and a small positional shake. Driven off <see cref="Health.Hit"/>,
    /// so it triggers the instant the blow lands, right before the global hitstop
    /// freezes time.
    ///
    /// Runs on unscaled time on purpose. Hitstop drops Time.timeScale to near zero,
    /// so a scaled-time flash would freeze with everything else and never show. On
    /// unscaled time the blink and shake play through the freeze-frame, which is
    /// exactly the "blink just before the hitstop" read you want.
    ///
    /// The shake is applied ADDITIVELY: each frame it removes last frame's offset
    /// and adds a fresh one, so it never fights the mover. Knockback (and the
    /// enemy's own movement) still carry through no matter how long the shake runs.
    /// </summary>
    public class EnemyHitReaction : MonoBehaviour
    {
        [Header("Blink")]
        [Tooltip("Renderers to flash. Auto-filled from children if left empty.")]
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color flashColor = Color.white;
        [Tooltip("How long the blink lasts (real seconds).")]
        [SerializeField] private float flashDuration = 0.08f;
        [Tooltip("Emission-style flashes read stronger; this just controls blend amount.")]
        [SerializeField, Range(0f, 1f)] private float flashStrength = 0.85f;

        [Header("Shake")]
        [Tooltip("What to shake. Defaults to this component's own transform. The shake " +
                 "is additive, so it's safe to leave this on the mover root; it won't " +
                 "cancel knockback. Point it at a model child only if you want the " +
                 "jitter isolated to the visuals.")]
        [SerializeField] private Transform shakeTarget;
        [Tooltip("Peak positional offset (metres).")]
        [SerializeField] private float shakeAmplitude = 0.12f;
        [Tooltip("How long the shake lasts (real seconds).")]
        [SerializeField] private float shakeDuration = 0.14f;
        [Tooltip("Heavy hits shake harder than light.")]
        [SerializeField] private float heavyShakeScale = 1.6f;

        private Health health;

        // Cached material instances and their original colours, per renderer.
        private readonly List<Material> mats = new();
        private readonly List<Color> baseColors = new();
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        private Transform shakeRoot;
        private Vector3 appliedOffset;   // the shake offset currently baked into localPosition

        private float flashEndTime = -1f;
        private float shakeEndTime = -1f;
        private float shakeDurActive;
        private float shakeAmpActive;

        private void Awake()
        {
            health = GetComponentInParent<Health>();
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<Renderer>();

            // Shake the assigned model transform; falls back to our own. Keep this
            // OFF the CharacterController root so it doesn't fight EnemyCombat/Knockback.
            shakeRoot = shakeTarget != null ? shakeTarget : transform;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var m = r.material; // instance, so we don't touch the shared asset
                mats.Add(m);
                baseColors.Add(ReadColor(m));
            }
        }

        private void OnEnable()
        {
            if (health != null) health.Hit += OnHit;
        }

        private void OnDisable()
        {
            if (health != null) health.Hit -= OnHit;
            RestoreColors();

            // Undo any shake offset so the transform isn't left nudged.
            if (shakeRoot != null) shakeRoot.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;
            shakeEndTime = -1f;
        }

        private void OnHit(HitInfo hit, HitResult result)
        {
            // Only flinch on blows that actually connect (not parries, which are
            // the attacker's problem). Blocks get a smaller, still-visible flinch.
            if (result == HitResult.Parried) return;

            float scale = hit.Type == AttackType.Heavy ? heavyShakeScale : 1f;
            if (result == HitResult.Blocked) scale *= 0.5f;

            flashEndTime = Time.unscaledTime + flashDuration;

            shakeAmpActive = shakeAmplitude * scale;
            shakeDurActive = shakeDuration;
            shakeEndTime = Time.unscaledTime + shakeDuration;
        }

        private void Update()
        {
            float now = Time.unscaledTime;

            // Flash: blend material colour toward the flash colour, then back.
            bool flashing = now < flashEndTime;
            float flashT = flashing ? flashStrength : 0f;
            for (int i = 0; i < mats.Count; i++)
            {
                if (mats[i] == null) continue;
                WriteColor(mats[i], Color.Lerp(baseColors[i], flashColor, flashT));
            }

            // Shake additively: remove last frame's offset, then add a fresh one.
            // This never fights the mover (Knockback / EnemyCombat), because the
            // net position each frame is "wherever they moved us" plus a small
            // jitter, instead of snapping back to a frozen home. So knockback still
            // carries through no matter how long the shake lasts.
            shakeRoot.localPosition -= appliedOffset;
            appliedOffset = Vector3.zero;

            if (now < shakeEndTime)
            {
                float remaining = (shakeEndTime - now) / Mathf.Max(0.0001f, shakeDurActive);
                appliedOffset = Random.insideUnitSphere * (shakeAmpActive * remaining);
                shakeRoot.localPosition += appliedOffset;
            }
        }

        private static Color ReadColor(Material m)
        {
            if (m.HasProperty(ColorId)) return m.GetColor(ColorId);
            if (m.HasProperty(LegacyColorId)) return m.GetColor(LegacyColorId);
            return Color.white;
        }

        private void WriteColor(Material m, Color c)
        {
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
            else if (m.HasProperty(LegacyColorId)) m.SetColor(LegacyColorId, c);
        }

        private void RestoreColors()
        {
            for (int i = 0; i < mats.Count; i++)
                if (mats[i] != null) WriteColor(mats[i], baseColors[i]);
        }
    }
}
