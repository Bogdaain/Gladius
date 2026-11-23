using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// A weapon as data. Everything that makes one weapon feel different from
    /// another (reach, damage, swing speed, stamina cost, parry window) lives here,
    /// so adding or retuning a weapon is editing an asset, not the combat code.
    /// Create via Assets > Create > Gladius > Weapon.
    /// </summary>
    [CreateAssetMenu(menuName = "Gladius/Weapon", fileName = "NewWeapon")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Sword";

        [Header("Reach (spherecast from the view)")]
        [Tooltip("How far in front the swing can connect.")]
        public float range = 2.2f;
        [Tooltip("Radius of the swept sphere. Wider = more forgiving aim.")]
        public float radius = 0.4f;
        [Tooltip("What the swing is allowed to hit. Exclude the attacker's own layer.")]
        public LayerMask hittableMask = ~0;

        [Header("Light attack")]
        public float lightDamage = 12f;
        [Tooltip("Time from input to the hit check landing.")]
        public float lightWindup = 0.12f;
        [Tooltip("Cooldown before the next attack can start.")]
        public float lightRecovery = 0.28f;
        public float lightStaminaCost = 10f;

        [Header("Heavy attack (hold to charge, release to swing)")]
        public float heavyDamage = 28f;
        public float heavyWindup = 0.32f;
        public float heavyRecovery = 0.55f;
        public float heavyStaminaCost = 25f;
        [Tooltip("Hold the attack button at least this long to start charging a heavy. " +
                 "Release before this and you throw a light attack instead.")]
        public float heavyHoldThreshold = 0.22f;
        [Tooltip("Hold time (from the start of the press) at which the heavy is fully charged.")]
        public float heavyFullChargeTime = 1f;
        [Tooltip("Damage multiplier at full charge. 1 = no scaling; 2 = double heavy damage.")]
        [Min(1f)] public float heavyMaxChargeMultiplier = 2f;

        /// <summary>
        /// Charge fraction (0..1) for a heavy held <paramref name="heldTime"/> seconds
        /// from the press. 0 at the moment charging begins, 1 at full charge.
        /// </summary>
        public float ChargeFraction(float heldTime)
        {
            float span = heavyFullChargeTime - heavyHoldThreshold;
            if (span <= 0f) return 1f;
            return Mathf.Clamp01((heldTime - heavyHoldThreshold) / span);
        }

        /// <summary>Damage multiplier for a heavy at the given charge fraction (0..1).</summary>
        public float ChargeDamageMultiplier(float chargeFraction) =>
            Mathf.Lerp(1f, heavyMaxChargeMultiplier, Mathf.Clamp01(chargeFraction));

        [Header("Block / Parry")]
        [Tooltip("Fraction of damage that gets through while blocking. 0.3 = take 30%.")]
        [Range(0f, 1f)] public float blockDamageMultiplier = 0.3f;
        [Tooltip("Blocking within this long of raising the guard counts as a parry.")]
        public float parryWindow = 0.18f;
        [Tooltip("How long a parried attacker is stunned.")]
        public float parryStunDuration = 1.2f;
        [Tooltip("Stamina drained from the holder each time a block absorbs a hit.")]
        public float blockStaminaCost = 12f;

        public float DamageFor(AttackType type) => type == AttackType.Heavy ? heavyDamage : lightDamage;
        public float WindupFor(AttackType type) => type == AttackType.Heavy ? heavyWindup : lightWindup;
        public float RecoveryFor(AttackType type) => type == AttackType.Heavy ? heavyRecovery : lightRecovery;
        public float StaminaCostFor(AttackType type) => type == AttackType.Heavy ? heavyStaminaCost : lightStaminaCost;
    }
}
