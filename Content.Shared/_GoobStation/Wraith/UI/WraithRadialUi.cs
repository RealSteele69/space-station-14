// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Fork note: source's Evolve/Spook/ChooseVoidCreature menus all used Content.Shared._White.RadialSelector
// (RadialSelectorEntry/RadialSelectorUiKey/TrackedRadialSelectorState/RadialSelectorSelectedMessage), a UI
// system from another fork that doesn't exist here. This fork already has a vanilla-upstream RadialMenu/
// SimpleRadialMenu control (used e.g. by the Heretic shapeshift menu and BloodCult's generic radial BUI at
// Content.Client/_ClawCommand/BloodCult/UI/BloodCultBoundUserInterfaces.cs). This file defines a small
// generic contract (entries/state/message) that lets every Wraith radial picker (evolution form, spook
// sub-action, void creature choice) share one client BoundUserInterface built on SimpleRadialMenu, each
// keyed by its own UiKey enum.

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._GoobStation.Wraith.UI;

/// <summary>
/// One selectable option in a Wraith radial menu.
/// </summary>
[Serializable, NetSerializable]
[DataDefinition]
public sealed partial class WraithRadialEntry
{
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField]
    public string? Name;

    [DataField]
    public EntProtoId? Prototype;

    [DataField]
    public SpriteSpecifier? Icon;

    public WraithRadialEntry() { }

    public WraithRadialEntry(string id, string? name = null, EntProtoId? prototype = null, SpriteSpecifier? icon = null)
    {
        Id = id;
        Name = name;
        Prototype = prototype;
        Icon = icon;
    }
}

[Serializable, NetSerializable]
public sealed class WraithRadialBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<WraithRadialEntry> Entries;

    public WraithRadialBoundUserInterfaceState(List<WraithRadialEntry> entries)
    {
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class WraithRadialSelectedMessage : BoundUserInterfaceMessage
{
    public string SelectedId;

    public WraithRadialSelectedMessage(string selectedId)
    {
        SelectedId = selectedId;
    }
}

[Serializable, NetSerializable]
public enum WraithEvolveUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum SpookRadialUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ChooseVoidCreatureUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum WraithDefileUiKey : byte
{
    Key,
}
