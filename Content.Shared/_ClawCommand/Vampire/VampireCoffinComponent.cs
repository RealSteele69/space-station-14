// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// Marks any closet/crate-like structure as a vampire's claimed coffin: their lair, and the one safe
/// place they can rest to heal quickly without spending blood. Claimed by closing the lid while a
/// vampire with no coffin of their own is resting inside.
/// </summary>
[RegisterComponent]
public sealed partial class VampireCoffinComponent : Component
{
    [DataField]
    public EntityUid? Claimant; // not "Owner": that would hide Component.Owner (CS0108, an error in Release)
}
