// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Basically just injects whatever chemical you want and breaks cuffs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlasherRegenerateComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEnt;

    [DataField]
    public EntProtoId ActionId = "ActionSlasherRegenerate";

    /// <summary>
    /// The reagent to inject.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "Slasherium";

    /// <summary>
    /// How much reagent to inject.
    /// </summary>
    [DataField]
    public float ReagentAmount = 10f;

    /// <summary>
    /// Whether the slasher has a stolen soul available to use for regenerate.
    /// Acts as ammo for the regenerate ability. Max of one soul at a time to prevent stacking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasSoulAvailable = true; // Start with one soul available

    /// <summary>
    /// The sound that plays when regenerating.
    /// </summary>
    [DataField]
    public SoundSpecifier? RegenerateSound = new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/SlasherRegenerate.ogg");

    /// <summary>
    /// The effect entity that is spawned when regenerating (includes light, sprite, and auto-despawn).
    /// </summary>
    [DataField]
    public EntProtoId RegenerateEffect = "SlasherRegenerateEffect";

    /// <summary>
    /// Range for the regenerate overlay.
    /// </summary>
    [DataField]
    public float RegenerateEffectRange = 10f;
}
