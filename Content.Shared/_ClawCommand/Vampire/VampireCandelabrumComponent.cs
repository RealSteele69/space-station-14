// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._ClawCommand.Mood;
using Robust.Shared.Prototypes;

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// A crypt fixture: while lit, it unsettles any living crew nearby who aren't bound to its owner.
/// Click it to toggle the light.
/// </summary>
[RegisterComponent]
public sealed partial class VampireCandelabrumComponent : Component
{
    [DataField]
    public bool Lit = true;

    [DataField]
    public float Range = 5f;

    [DataField]
    public ProtoId<MoodEffectPrototype> Moodlet = "VampireCandelabrumUnease";

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan NextTick;
}
