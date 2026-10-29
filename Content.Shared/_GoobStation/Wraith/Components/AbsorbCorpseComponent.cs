// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class AbsorbCorpseComponent : Component
{
    /// <summary>
    /// The amount of corpses that have already been absorbed
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CorpsesAbsorbed;

    [DataField]
    public EntProtoId SmokeProto = "AdminInstantEffectSmoke3";

    /// <summary>
    /// How much to add to the generation rate of WP of the entity
    /// </summary>
    [DataField]
    public FixedPoint2 WpPassiveAdd = 0.4;

    /// <summary>
    /// If the target has this much of X chem in their body, it hurts the wraith.
    /// </summary>
    [DataField]
    public FixedPoint2 FormaldehydeThreshhold = 25;

    /// <summary>
    /// Removes all the formaldehyde from their system.
    /// </summary>
    [DataField]
    public FixedPoint2 ChemToRemove = 999;

    /// <summary>
    ///  Damage to deal to the wraith.
    /// </summary>
    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Formaldehyde";

    /// <summary>
    /// Sounds to be played when wraith absorbs someone.
    /// </summary>
    // Fork note: source's sound collection was "Wraith_SoulSucc"; this fork's
    // Resources/Prototypes/_GoobStation/Wraith/Audio/wraith_sound_collection.yml defines it
    // without the underscore as "WraithSoulSucc".
    [DataField]
    public SoundSpecifier? AbsorbSound = new SoundCollectionSpecifier("WraithSoulSucc");

    [ViewVariables]
    public ProtoId<TagPrototype> Tag = "VimPilot";
}

/// <summary>
/// Raised on user once trying to absorb a corpse
/// </summary>
[ByRefEvent]
public record struct AbsorbCorpseAttemptEvent(EntityUid Target, bool Handled = false, bool Cancelled = false);
