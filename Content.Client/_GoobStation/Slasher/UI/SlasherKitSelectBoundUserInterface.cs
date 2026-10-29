// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: source used a "this.CreateWindow<T>()" extension method that doesn't exist on this
// fork. Rebuilt window creation/disposal using the plain "new MenuType(); OpenCentered();
// Dispose(bool) closes it" pattern this fork's other custom BUIs already use (e.g.
// Content.Client._ClawCommand.Blob.BlobChemSwapBoundUserInterface).

using Content.Shared._GoobStation.Slasher.UI;
using JetBrains.Annotations;

namespace Content.Client._GoobStation.Slasher.UI;

[UsedImplicitly]
public sealed class SlasherKitSelectBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SlasherKitSelectMenu? _window;

    public SlasherKitSelectBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new SlasherKitSelectMenu();
        _window.OnKitSelected += index =>
        {
            SendMessage(new SlasherKitSelectedMessage(index));
            _window.ForceClose();
        };
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SlasherKitSelectBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _window?.ForceClose();
            _window = null;
        }
    }
}
