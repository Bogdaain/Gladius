using UnityEngine;

namespace Gladius.Combat
{
    /// <summary>
    /// Everything a defender needs to know about an incoming blow. Passed into
    /// <see cref="IDamageable.TakeHit"/> so the receiver can decide how it reacts
    /// (block, parry, flinch, bleed) and where to spawn feedback.
    /// </summary>
    public struct HitInfo
    {
        /// <summary>Damage before the defender applies its own block reduction.</summary>
        public float Damage;

        /// <summary>Light or heavy. Drives clash rules and reaction strength.</summary>
        public AttackType Type;

        /// <summary>Who swung. May be null for environmental or self-inflicted damage.</summary>
        public GameObject Attacker;

        /// <summary>World-space point the blow landed, for blood and impact effects.</summary>
        public Vector3 Point;

        /// <summary>Direction the blow travelled, for knockback and spray direction.</summary>
        public Vector3 Direction;

        public HitInfo(float damage, AttackType type, GameObject attacker, Vector3 point, Vector3 direction)
        {
            Damage = damage;
            Type = type;
            Attacker = attacker;
            Point = point;
            Direction = direction;
        }
    }

    /// <summary>
    /// How a defender ended up responding to a blow. Returned from
    /// <see cref="IDamageable.TakeHit"/> so the attacker can react in kind, e.g.
    /// recoil off a block or get stunned by a parry.
    /// </summary>
    public enum HitResult
    {
        /// <summary>Hit landed and dealt full damage.</summary>
        Hit,

        /// <summary>Defender was blocking: reduced damage, no stun.</summary>
        Blocked,

        /// <summary>Defender blocked at the perfect moment: no damage, attacker is stunned.</summary>
        Parried
    }
}
