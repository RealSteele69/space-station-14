using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._ClawCommand.CosmicCult.Components;

[RegisterComponent]
[AutoGenerateComponentPause]
public sealed partial class InVoidComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan ExitVoidTime = default!;

    [DataField]
    public EntityUid OriginalBody;
}
