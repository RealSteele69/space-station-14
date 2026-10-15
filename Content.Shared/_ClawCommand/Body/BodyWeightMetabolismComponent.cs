namespace Content.Shared._ClawCommand.Body;

/// <summary>
///     Claw Command - Server-side bookkeeping for the body-weight hunger/thirst burn rate.
///
///     The old Hunger/Thirst components carried a per-entity decay rate that
///     <see cref="BodyWeightSystem"/> simply rescaled. Satiation keeps its change rate on the shared
///     prototype instead, so the weight difference is applied as a periodic top-up or extra drain on
///     top of the natural decay. Only present while the build actually differs from the default.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class BodyWeightMetabolismComponent : Component
{
    /// <summary>
    ///     Multiplier on the natural hunger and thirst decay. 1.25 burns a quarter faster.
    /// </summary>
    [ViewVariables]
    public float Factor = 1f;

    /// <summary>
    ///     How often the scaled decay is settled. Each settle dirties the satiation component, so this
    ///     is kept coarse; the per-step amount is a fraction of a point either way.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(10);

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate;
}
