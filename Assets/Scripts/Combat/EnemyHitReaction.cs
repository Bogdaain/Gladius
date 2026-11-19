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
        [Tooltip("What to shake. Point this at the enemy's MODEL child, not the root " +
                 "with the CharacterController, or the shake will fight the mover. " +
                 "Defaults to this component's own transform.")]
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
        private Vector3 shakeHome;
        private bool homeStored;

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

            if (!homeStored)
            {
                shakeHome = shakeRoot.localPosition;
                homeStored = true;
            }
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

            // Shake: random offset that decays to nothing, then snap home.
            if (now < shakeEndTime)
            {
                float remaining = (shakeEndTime - now) / Mathf.Max(0.0001f, shakeDurActive);
                Vector3 offset = Random.insideUnitSphere * (shakeAmpActive * remaining);
                shakeRoot.localPosition = shakeHome + offset;
            }
            else if (homeStored)
            {
                shakeRoot.localPosition = shakeHome;
                homeStored = false;
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
