// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._GoobStation.Wraith.Curses;

/// <summary>
/// This component marks the user as an entity that has Curses on them
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class CurseHolderComponent : Component
{
    /// <summary>
    /// The curses the user currently has.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<CursePrototype>> ActiveCurses = new();

    /// <summary>
    ///  Stores the timespans of the curses
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<ProtoId<CursePrototype>, TimeSpan> CurseUpdate = new();

    [ViewVariables, AutoNetworkedField]
    public List<ProtoId<CurseStatusIconPrototype>> CurseStatusIcons = new();

    /// <summary>
    /// The entity that cursed us
    /// </summary>
    [ViewVariables]
    public EntityUid? Curser;
}
