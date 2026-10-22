using Robust.Shared.Configuration;

namespace Content.Shared._ClawCommand.CCVar;

public sealed partial class ClawCCVars
{
    /// <summary>
    /// Chance that an infected (but not initial infected) entity develops a critical infection
    /// over the course of <see cref="ZombieCriticalInfectionWindow"/>. 0 disables critical infections.
    /// </summary>
    public static readonly CVarDef<float> ZombieCriticalInfectionChance =
        CVarDef.Create("zombie.critical_infection_chance", 0.10f, CVar.SERVERONLY);

    /// <summary>
    /// The window, in seconds, that <see cref="ZombieCriticalInfectionChance"/> is spread over.
    /// </summary>
    public static readonly CVarDef<float> ZombieCriticalInfectionWindow =
        CVarDef.Create("zombie.critical_infection_window", 1800f, CVar.SERVERONLY);
}
