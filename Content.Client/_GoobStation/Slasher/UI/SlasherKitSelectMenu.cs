// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note (simplification): source decorated this window with a Heretic "cloud_swirl" texture
// and an animated "rnd-static" TV-static background behind the scroll view. Rebuilt with this
// fork's standard Nano panel textures only (no extra decorative RSI dependency) - same layout,
// same horizontal scrolling kit list, just plainer chrome.

using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared._GoobStation.Slasher.UI;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._GoobStation.Slasher.UI;

/// <summary>
/// Horizontal kit selection window for the Slasher.
/// </summary>
public sealed partial class SlasherKitSelectMenu : FancyWindow
{
    [Dependency] private EntityManager _entManager = default!;

    private readonly SpriteSystem _spriteSystem = default!;

    private readonly BoxContainer _kitsContainer;

    public event Action<int>? OnKitSelected;

    public SlasherKitSelectMenu()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entManager.System<SpriteSystem>();

        var textColor = Color.FromHex("#fff5f8");

        StyleBoxTexture MakeTexture(SpriteSpecifier sprite, string color)
        {
            var texture = new StyleBoxTexture
            {
                Texture = _spriteSystem.Frame0(sprite),
                Modulate = Color.FromHex(color),
                Mode = StyleBoxTexture.StretchMode.Stretch,
            };
            texture.SetPatchMargin(StyleBox.Margin.All, 2);
            return texture;
        }

        var frameTexture = MakeTexture(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/transparent_window_background_bordered.png")), "#ff9caf");
        var rootTexture = MakeTexture(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/window_background_bordered.png")), "#1a0c10");
        var headerTexture = MakeTexture(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/window_header_alert.png")), "#d61f49");
        var panelTexture = MakeTexture(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/black_panel_red_thin_border.png")), "#ffb0be");
        var insetTexture = MakeTexture(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/light_panel_background_bordered.png")), "#4b1e29");

        Title = Loc.GetString("slasher-kit-select-title");
        HideCloseButton(this);
        MinSize = new System.Numerics.Vector2(720, 525);

        var frame = new PanelContainer
        {
            Margin = new Thickness(4, 30, 4, 4),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = frameTexture,
        };

        var rootPanel = new PanelContainer
        {
            Margin = new Thickness(3),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = rootTexture,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(8, 18, 8, 8),
        };

        var headerPanel = new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 8),
            MinHeight = 42,
            PanelOverride = headerTexture,
        };

        var headerColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(8, 6, 8, 5),
        };

        var header = new Label
        {
            Text = Loc.GetString("slasher-kit-select-header"),
            HorizontalAlignment = HAlignment.Left,
            FontColorOverride = textColor,
            StyleClasses = { "StatusFieldTitle" },
        };

        var scrollFrame = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = panelTexture,
        };

        var scrollInset = new PanelContainer
        {
            Margin = new Thickness(3),
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = insetTexture,
        };

        var scroll = new ScrollContainer
        {
            HScrollEnabled = true,
            VScrollEnabled = false,
            VerticalExpand = true,
            HorizontalExpand = true,
            Margin = new Thickness(6, 6, 6, 6),
        };

        _kitsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            VerticalExpand = true,
            SeparationOverride = 6,
            Margin = new Thickness(2),
        };

        headerColumn.AddChild(header);
        headerPanel.AddChild(headerColumn);
        scroll.AddChild(_kitsContainer);
        scrollInset.AddChild(scroll);
        scrollFrame.AddChild(scrollInset);
        root.AddChild(headerPanel);
        root.AddChild(scrollFrame);
        rootPanel.AddChild(root);
        frame.AddChild(rootPanel);
        AddChild(frame);
    }

    private static void HideCloseButton(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is TextureButton { Name: "CloseButton" } closeButton)
            {
                closeButton.Visible = false;
                return;
            }

            HideCloseButton(child);
        }
    }

    public override void Close() { }
    public void ForceClose() => base.Close();

    public void UpdateState(SlasherKitSelectBoundUserInterfaceState state)
    {
        _kitsContainer.DisposeAllChildren();

        for (var i = 0; i < state.Kits.Count; i++)
        {
            if (i > 0)
            {
                var separatorGlow = new PanelContainer
                {
                    MinWidth = 28,
                    MaxWidth = 28,
                    MinHeight = 3,
                    MaxHeight = 3,
                    VerticalAlignment = VAlignment.Center,
                    HorizontalExpand = false,
                    Margin = new Thickness(4, 0),
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#ff2a92"),
                    },
                };

                _kitsContainer.AddChild(separatorGlow);
            }

            var index = i;
            var kit = state.Kits[i];
            var card = new SlasherKitCard(kit, _spriteSystem);
            card.SelectButton.OnPressed += _ => OnKitSelected?.Invoke(index);
            _kitsContainer.AddChild(card);
        }
    }
}
