using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.MobCall;

/// <summary>
///     Claw Command - Lets this entity use a <see cref="MobCallActionEvent"/> action to call nearby
///     <see cref="MobCallableComponent"/> mobs (e.g. mice) to where it is standing.
/// </summary>
[RegisterComponent]
public sealed partial class MobCallSourceComponent : Component
{
    /// <summary>
    ///     Which callable mobs answer.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField]
    public float Range = 20f;

    /// <summary>
    ///     NPC blackboard key the call location is written to; the mob's HTN walks to it.
    /// </summary>
    [DataField]
    public string Key = "CallTarget";

    [DataField]
    public ProtoId<EmotePrototype> Emote = "Scream";
}

/// <summary>
///     Claw Command - Marks a mob that comes when a <see cref="MobCallSourceComponent"/> calls.
/// </summary>
[RegisterComponent]
public sealed partial class MobCallableComponent : Component;

public sealed partial class MobCallActionEvent : InstantActionEvent;
