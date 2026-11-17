// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Speech.EntitySystems;
using Content.Shared.Speech.Components;

namespace Content.Server.Speech.Components;

/// <summary>
/// Speaks in olde tongue, with the occasional "Hark!" thrown in front.
/// </summary>
[RegisterComponent]
[Access(typeof(MedievalAccentSystem))]
public sealed partial class MedievalAccentComponent : BaseAccentComponent
{
    [DataField]
    public float PrefixChance = 0.4f;

    /// <summary>
    /// How many accent-medieval-prefix-N strings exist.
    /// </summary>
    [DataField]
    public int PrefixCount = 41;
}
