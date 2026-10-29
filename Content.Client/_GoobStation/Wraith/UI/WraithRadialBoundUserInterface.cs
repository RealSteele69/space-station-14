// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// Generic radial-menu BUI shared by every Wraith picker (evolution form, spook sub-action, void creature
// choice, defile reagent) - see Content.Shared._GoobStation.Wraith.UI.WraithRadialUi.cs for why. Modeled
// directly on this fork's BloodCultRadialBoundUserInterface
// (Content.Client._ClawCommand.BloodCult.UI.BloodCultBoundUserInterfaces.cs), which already does the same
// "server sends a list of (id, name, icon) entries, client shows a SimpleRadialMenu, clicking sends the id
// back" pattern for an unrelated antag.

using System.Linq;
using Content.Shared._GoobStation.Wraith.UI;
using Content.Client.UserInterface.Controls;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._GoobStation.Wraith.UI;

[UsedImplicitly]
public sealed partial class WraithRadialBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        if (State is WraithRadialBoundUserInterfaceState state)
            _menu.SetButtons(CreateButtons(state));

        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is WraithRadialBoundUserInterfaceState radial)
            _menu?.SetButtons(CreateButtons(radial));
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons(WraithRadialBoundUserInterfaceState state)
    {
        return state.Entries.Select((entry, i) => (RadialMenuOptionBase)new RadialMenuActionOption<string>(Select, entry.Id)
        {
            ToolTip = entry.Name ?? entry.Id,
            Order = i,
            IconSpecifier = entry.Icon is { } icon
                ? RadialMenuIconSpecifier.With(icon)
                : RadialMenuIconSpecifier.With(entry.Prototype),
        });
    }

    private void Select(string id)
    {
        SendMessage(new WraithRadialSelectedMessage(id));
        Close();
    }
}
