using Content.Shared.Antag;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.AdvancedXenos.Rules;

/// <summary>
/// Claw Command - for xenos an admin spawns by hand. When a ghost takes the mob, the player becomes a
/// full xenomorph antagonist (briefing, objectives, round-end summary) through <see cref="Rule"/>,
/// which is started on first use. The mob stays where the admin put it.
/// </summary>
[RegisterComponent, Access(typeof(AdvXenoAdminSpawnSystem))]
public sealed partial class AdvXenoAdminSpawnComponent : Component
{
    [DataField]
    public EntProtoId Rule = "AdvancedXenomorphAdmin";

    [DataField]
    public ProtoId<AntagSpecifierPrototype> Antag = "AdvancedXenomorphAdmin";
}
