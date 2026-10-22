namespace Content.Server._ClawCommand.Zombies;

/// <summary>
/// Records the one-time roll for whether this entity can ever go critical from a zombie infection.
/// Added the first time the entity is infected while eligible, and never rerolled, so getting cured
/// and bitten again keeps the same outcome.
/// </summary>
[RegisterComponent, Access(typeof(CriticalZombieInfectionSystem))]
public sealed partial class CriticalInfectionResistanceComponent : Component
{
    /// <summary>
    /// If true the entity can still be infected and turn the normal way, but never goes critical.
    /// </summary>
    [DataField]
    public bool Resistant;
}
