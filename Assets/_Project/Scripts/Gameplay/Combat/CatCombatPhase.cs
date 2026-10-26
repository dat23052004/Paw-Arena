namespace PawArena.Gameplay.Combat
{
    /// <summary>Exclusive physical action currently owned by one combatant.</summary>
    public enum CatCombatPhase
    {
        Free,
        Punch,
        Grab,
        Holding,
        Held,
        Lifting,
        Carrying,
        Carried,
        Throwing,
        Ability,
        HitStun,
        KnockedOut
    }
}
