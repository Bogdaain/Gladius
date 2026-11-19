using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Executes a single swing with a spherecast and resolves what it meets.
    /// Kept separate from the combat brains so player and enemy both drive their
    /// swings through the same reach, clash, and hit resolution.
    ///
    /// Swings fire from <see cref="origin"/> (the camera for the player, the head
    /// for an enemy) along its forward. A swept sphere is forgiving for a fast FPS
    /// and reuses the spherecast pattern already in the movement controller.
    /// </summary>
    public class MeleeHitbox : MonoBehaviour
    {
        [Tooltip("Where the swing is measured from. Camera for the player, head/chest for an enemy.")]
        [SerializeField] private Transform origin;

        /// <summary>Result of a single swing, so the attacker can react (recoil, stun).</summary>
        public enum SwingOutcome { Miss, Hit, Blocked, Parried, Clashed }

        /// <summary>
        /// Perform one swing. <paramref name="owner"/> is the attacker's root
        /// (so it never hits itself and shows up as the attacker in HitInfo).
        /// </summary>
        [Tooltip("Half-angle (degrees) of the cone in front of the origin a swing can land in. " +
                 "90 = anything to the sides; smaller = must aim more directly.")]
        [SerializeField, Range(10f, 120f)] private float swingHalfAngle = 70f;

        // Reused buffer so a swing allocates nothing.
        private readonly Collider[] overlaps = new Collider[16];

        public SwingOutcome Swing(WeaponData weapon, AttackType type, GameObject owner)
        {
            if (weapon == null || origin == null) return SwingOutcome.Miss;

            Vector3 start = origin.position;
            Vector3 dir = origin.forward;
            float damage = weapon.DamageFor(type);

            // Overlap a sphere centred partway down the reach. Unlike SphereCast,
            // OverlapSphere detects targets already touching us at melee range
            // (no start-overlap dead zone), which is why close hits were whiffing.
            Vector3 center = start + dir * (weapon.range * 0.5f);
            float reach = weapon.range * 0.5f + weapon.radius;

            int count = Physics.OverlapSphereNonAlloc(center, reach, overlaps,
                weapon.hittableMask, QueryTriggerInteraction.Collide);

            // Resolve against the nearest valid target within the swing cone.
            Collider best = null;
            float bestSqr = float.MaxValue;
            float cosHalf = Mathf.Cos(swingHalfAngle * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i];
                if (c == null || c.transform.root == owner.transform.root) continue;

                Vector3 toTarget = c.bounds.ClosestPoint(start) - start;
                float distSqr = toTarget.sqrMagnitude;
                if (distSqr > 0.0001f)
                {
                    // Must be roughly in front: inside the swing cone.
                    if (Vector3.Dot(dir, toTarget.normalized) < cosHalf) continue;
                }
                if (distSqr < bestSqr) { bestSqr = distSqr; best = c; }
            }

            if (best == null) return SwingOutcome.Miss;

            // A live enemy weapon mid-swing can clash with ours.
            var otherHitbox = best.GetComponentInParent<MeleeHitbox>();
            if (otherHitbox != null && otherHitbox != this)
            {
                var brain = otherHitbox.GetComponentInParent<IClashable>();
                if (brain != null && brain.IsSwinging && ClashCancels(type, brain.CurrentAttack))
                {
                    var self = GetComponentInParent<IClashable>();
                    brain.OnClash(type);
                    self?.OnClash(brain.CurrentAttack);
                    return SwingOutcome.Clashed;
                }
            }

            var target = best.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive) return SwingOutcome.Miss;

            Vector3 point = best.ClosestPoint(center);
            var info = new HitInfo(damage, type, owner, point, dir);
            HitResult result = target.TakeHit(info);

            if (result == HitResult.Parried)
            {
                // The defender parried us. Stun the attacker (us) for the weapon
                // that defended, if it can be found; fall back to ours.
                var defenderWeapon = (target as Component)?.GetComponentInParent<IGuardWeaponSource>();
                float stun = defenderWeapon?.GuardWeapon != null
                    ? defenderWeapon.GuardWeapon.parryStunDuration
                    : weapon.parryStunDuration;
                owner.GetComponentInParent<IStunnable>()?.ApplyParryStun(stun);
            }

            return result switch
            {
                HitResult.Parried => SwingOutcome.Parried,
                HitResult.Blocked => SwingOutcome.Blocked,
                _ => SwingOutcome.Hit
            };
        }

        /// <summary>
        /// Clash rule: only like meets like. Light cancels Light, Heavy cancels
        /// Heavy. A Light swung into a Heavy does NOT cancel it, so the lighter
        /// attacker loses and the heavy swing carries through.
        /// </summary>
        private static bool ClashCancels(AttackType a, AttackType b) => a == b;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (origin == null) return;
            Gizmos.color = Color.red;
            // A rough reach preview; range/radius come from the weapon at runtime.
            Gizmos.DrawLine(origin.position, origin.position + origin.forward * 2.2f);
            Gizmos.DrawWireSphere(origin.position + origin.forward * 2.2f, 0.4f);
        }
#endif
    }

    /// <summary>
    /// Implemented by combat brains (player, enemy) so a swing can tell whether
    /// the thing it met is mid-swing and, if so, cancel against it.
    /// </summary>
    public interface IClashable
    {
        bool IsSwinging { get; }
        AttackType CurrentAttack { get; }

        /// <summary>Our swing was cancelled by an opposing swing of the given type.</summary>
        void OnClash(AttackType incoming);
    }

    /// <summary>Implemented by combat brains that can be stunned by a parry.</summary>
    public interface IStunnable
    {
        void ApplyParryStun(float duration);
    }

    /// <summary>Lets a parry read the defender's weapon for its stun duration.</summary>
    public interface IGuardWeaponSource
    {
        WeaponData GuardWeapon { get; }
    }
}
