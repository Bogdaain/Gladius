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

        [Header("Heavy attack (hold)")]
        public float heavyDamage = 28f;
        public float heavyWindup = 0.32f;
        public float heavyRecovery = 0.55f;
        public float heavyStaminaCost = 25f;
        [Tooltip("Hold the attack button at least this long to commit to a heavy.")]
        public float heavyHoldThreshold = 0.22f;

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
