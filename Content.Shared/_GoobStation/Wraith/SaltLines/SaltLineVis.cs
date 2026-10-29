// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Serialization;

namespace Content.Shared._GoobStation.Wraith.SaltLines;

[Serializable, NetSerializable]
public enum SaltLineVisuals
{
    ConnectedMask,
}

[Flags]
[Serializable, NetSerializable]
public enum SaltLineVisDirFlags : byte
{
    None = 0,
    North = 1,
    South = 2,
    East = 4,
    West = 8,
}
