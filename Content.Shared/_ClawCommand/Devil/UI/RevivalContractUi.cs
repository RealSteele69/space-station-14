// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.Devil.UI;

[Serializable, NetSerializable]
public sealed class RevivalContractMessage(bool accepted) : BoundUserInterfaceMessage
{
    public bool Accepted { get; } = accepted;
}

[Serializable, NetSerializable]
public enum RevivalContractUiKey
{
    Key,
}
