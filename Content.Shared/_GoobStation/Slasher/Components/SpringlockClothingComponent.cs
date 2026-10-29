// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.

using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// When the entity wearing this clothing item is touched by water it
/// changes to a locked state and makes the wearer scream.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SpringlockClothingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsLocked;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? LockSound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");

    [DataField, AutoNetworkedField]
    public Dictionary<string, List<PrototypeLayerData>> LockedClothingVisuals = new();

    [DataField, AutoNetworkedField]
    public string LockedSpriteLayer = "locked";
}

[Serializable, NetSerializable]
public enum SpringlockVisuals : byte
{
    Locked,
}
