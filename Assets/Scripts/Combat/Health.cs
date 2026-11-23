using System;
using UnityEngine;
using UnityEngine.Events;

namespace Gladius.Combat
{
    /// <summary>
    /// A health pool that knows how to receive a blow. Shared by the player and
    /// every enemy. It owns the block/parry decision: a guard raised within the
    /// weapon's parry window turns an incoming hit into a parry (no damage, the
    /// attacker gets stunned); a guard held past that window blocks (reduced
    /// damage). Everything else takes full damage.
    ///
    /// Like the movement scripts, all feedback goes out through UnityEvents so
    /// audio/VFX hook in without this script knowing they exist.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Pool")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Hit reactions")]
        [Tooltip("Can't be hit again for this long after taking a blow. 0 = no mercy.")]
        [SerializeField] private float invulnerabilityTime = 0.05f;

        [Header("Guard (set by a combat script, not in the inspector)")]
        [Tooltip("Optional. If assigned, block/parry uses this weapon's windows. " +
                 "Enemies without a weapon simply can't block.")]
        [SerializeField] private WeaponData defaultWeapon;

        /// <summary>Fires on any damage that gets through: (current, max).</summary>
        public event Action<float, float> Changed;

        /// <summary>
        /// Fires after any blow resolves, carrying the full hit and how it landed.
        /// Knockback and FX use this for direction and strength. Includes blocked
        /// and parried hits, so listeners can react differently to each.
        /// </summary>
        public event Action<HitInfo, HitResult> Hit;

        [Header("Events (hook audio / VFX / blood here)")]
        [Tooltip("Took real damage. Vector3 = world hit point.")]
        public UnityEvent<Vector3> onDamaged;
        [Tooltip("A blow was blocked (reduced damage got through). Vector3 = hit point.")]
        public UnityEvent<Vector3> onBlocked;
        [Tooltip("A blow was parried. Vector3 = hit point.")]
        public UnityEvent<Vector3> onParried;
        public UnityEvent onDied;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsAlive => Current > 0f;

        // Guard state, driven by a combat script via SetGuard.
        private bool guardRaised;
        private float guardRaisedTime = -100f;
        private WeaponData guardWeapon;

        private float lastHitTime = -100f;

        private void Awake()
        {
            Current = maxHealth;
            guardWeapon = defaultWeapon;
        }

        private void Start() => Changed?.Invoke(Current, maxHealth);

        /// <summary>
        /// Called by a combat script each time the guard goes up or down. The
        /// weapon supplies the parry window and block reduction. Raising the guard
        /// stamps the time so the parry window can be measured from this instant.
        /// </summary>
        public void SetGuard(bool raised, WeaponData weapon)
        {
            if (raised && !guardRaised) guardRaisedTime = Time.time;
            guardRaised = raised;
            if (weapon != null) guardWeapon = weapon;
        }

        public HitResult TakeHit(in HitInfo hit)
        {
            if (!IsAlive) return HitResult.Hit;
            if (Time.time - lastHitTime < invulnerabilityTime) return HitResult.Hit;
            lastHitTime = Time.time;

            HitResult result = Resolve(hit);
            Hit?.Invoke(hit, result);
            return result;
        }

        private HitResult Resolve(in HitInfo hit)
        {
            if (guardRaised && guardWeapon != null && FacingAttacker(hit))
            {
                bool withinParry = Time.time - guardRaisedTime <= guardWeapon.parryWindow;
                if (withinParry)
                {
                    onParried?.Invoke(hit.Point);
                    return HitResult.Parried;
                }

                float reduced = hit.Damage * guardWeapon.blockDamageMultiplier;
                ApplyDamage(reduced, hit.Point);
                onBlocked?.Invoke(hit.Point);
                return HitResult.Blocked;
            }

            ApplyDamage(hit.Damage, hit.Point);
            onDamaged?.Invoke(hit.Point);
            return HitResult.Hit;
        }

        /// <summary>A guard only counts if the blow comes from roughly the front.</summary>
        private bool FacingAttacker(in HitInfo hit)
        {
            if (hit.Attacker == null) return true;
            Vector3 toAttacker = hit.Attacker.transform.position - transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return true;
            return Vector3.Dot(transform.forward, toAttacker.normalized) > 0.2f;
        }

        private void ApplyDamage(float amount, Vector3 point)
        {
            if (amount <= 0f) return;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, maxHealth);

            if (Current <= 0f) onDied?.Invoke();
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }
    }
}
