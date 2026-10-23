namespace PawArena.Gameplay.Inventory
{
    /// <summary>
    /// Describes how an active ability resolves its target.
    /// </summary>
    public enum AbilityTargetingMode
    {
        Self,
        Direction,
        NearestOpponent,
        Area,
        Projectile
    }
}
