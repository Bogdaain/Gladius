namespace Gladius.Combat
{
    /// <summary>
    /// The kind of swing. Clash rules compare these: a Light meets a Light and a
    /// Heavy meets a Heavy, but a Light that runs into a Heavy loses outright.
    /// </summary>
    public enum AttackType
    {
        Light,
        Heavy
    }
}
