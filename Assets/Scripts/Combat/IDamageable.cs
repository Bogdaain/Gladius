namespace Gladius.Combat
{
    /// <summary>
    /// Anything a swing can land on. The melee hitbox searches for this on what
    /// it overlaps, so players, enemies, and destructible props all answer the
    /// same call. Implementations decide whether they block, parry, or just take it.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>Resolve an incoming blow and report how it was received.</summary>
        HitResult TakeHit(in HitInfo hit);

        /// <summary>False once dead, so swings and clashes can skip corpses.</summary>
        bool IsAlive { get; }
    }
}
