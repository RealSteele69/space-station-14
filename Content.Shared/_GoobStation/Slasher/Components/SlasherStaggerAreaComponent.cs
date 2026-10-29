// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Grants the Slasher an area stagger action that slows nearby enemies briefly.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlasherStaggerAreaComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEnt;

    [DataField]
    public EntProtoId ActionId = "ActionSlasherStaggerArea";

    /// <summary>
    /// Radius of the aura effect, and the radius the shockwave ring expands to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 3.5f;

    /// <summary>
    /// Shader the shockwave ring draws with.
    /// </summary>
    [DataField]
    public string ShockwaveShader = "SlasherStagger";

    [DataField]
    public Color RingColor = new(0.22f, 0.02f, 0.34f);

    /// <summary>
    /// Duration of the slowdown.
    /// </summary>
    [DataField]
    public float SlowDuration = 8f;

    /// <summary>
    /// Speed debuff.
    /// </summary>
    [DataField]
    public float SlowMultiplier = 0.5f;

    /// <summary>
    /// Sound to play when the stagger area is activated.
    /// </summary>
    [DataField]
    public SoundSpecifier StaggerSound = new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/SlasherStaggerArea.ogg")
    {
        Params = AudioParams.Default.WithMaxDistance(4f),
    };
}
