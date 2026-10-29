// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._GoobStation.Slasher.UI;

[Serializable, NetSerializable]
public enum SlasherKitSelectUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SlasherKitSelectBoundUserInterfaceState(List<SlasherKitInfo> kits) : BoundUserInterfaceState
{
    public readonly List<SlasherKitInfo> Kits = kits;
}

[Serializable, NetSerializable]
public sealed class SlasherKitSelectedMessage(int index) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
}

[Serializable, NetSerializable]
public sealed class SlasherKitInfo(
    string id,
    string name,
    string description,
    SpriteSpecifier sprite,
    string? guide)
{
    public string Id = id;
    public string Name = name;
    public string Description = description;
    public SpriteSpecifier Sprite = sprite;
    public string? Guide = guide;
}
