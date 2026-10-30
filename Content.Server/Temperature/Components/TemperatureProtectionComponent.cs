using Content.Server.Temperature.Systems;

namespace Content.Server.Temperature.Components;

[RegisterComponent]
[Access(typeof(TemperatureSystem))]
public sealed partial class TemperatureProtectionComponent : Component
{
    /// <summary>
    ///     Multiplier for the transferred heat when heating up or cooling down.
    /// </summary>
    [DataField]
    public float Coefficient = 1.0f;

    // Claw Command start - one-way insulation, e.g. winter gear that keeps cold out but still lets heat in.
    /// <summary>
    ///     Overrides <see cref="Coefficient"/> while the entity is gaining heat.
    /// </summary>
    [DataField]
    public float? HeatingCoefficient;

    /// <summary>
    ///     Overrides <see cref="Coefficient"/> while the entity is losing heat.
    /// </summary>
    [DataField]
    public float? CoolingCoefficient;
    // Claw Command end
}

/// <summary>
/// Event raised on an entity with <see cref="TemperatureProtectionComponent"/> to determine the actual value of the coefficient.
/// </summary>
[ByRefEvent]
public record struct GetTemperatureProtectionEvent(float Coefficient);
