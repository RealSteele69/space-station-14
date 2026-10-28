// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._ClawCommand.Vampire;

/// <summary>
/// While active, the vampire looks and reads as alive - at the cost of being unable to heal at all.
/// </summary>
[RegisterComponent]
public sealed partial class VampireMasqueradeComponent : Component;
