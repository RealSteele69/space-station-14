using System.Linq;
using Content.Client.Construction;
using Content.Client.UserInterface.Controls;
using Content.Shared._ClawCommand.BloodCult;
using Content.Shared._ClawCommand.BloodCult.Components;
using Content.Shared.Construction.Prototypes;
using JetBrains.Annotations;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._ClawCommand.BloodCult.UI;

/// <summary>
/// Generic cult radial menu, filled by a <see cref="BloodCultRadialState"/> from the server.
/// </summary>
[UsedImplicitly]
public sealed partial class BloodCultRadialBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);

        if (State is BloodCultRadialState state)
            _menu.SetButtons(CreateButtons(state));

        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BloodCultRadialState radial)
            _menu?.SetButtons(CreateButtons(radial));
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons(BloodCultRadialState state)
    {
        return state.Entries.Select((entry, i) => (RadialMenuOptionBase) new RadialMenuActionOption<string>(Select, entry.Id)
        {
            ToolTip = entry.Name,
            Order = i,
            IconSpecifier = entry.Icon is { } icon
                ? RadialMenuIconSpecifier.With(icon)
                : RadialMenuIconSpecifier.With(entry.Prototype),
        });
    }

    private void Select(string id)
    {
        SendMessage(new BloodCultOptionSelectedMessage(id));
        Close();
    }
}

/// <summary>
/// Generic cult list menu, used for picking runes and cultists by name.
/// </summary>
[UsedImplicitly]
public sealed partial class BloodCultListBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private FancyWindow? _window;
    private BoxContainer? _list;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FancyWindow>();
        _window.Title = Loc.GetString("blood-cult-list-title");
        _window.MinSize = new(280, 200);

        _list = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new(4),
        };

        _window.ContentsContainer.AddChild(new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _list },
        });

        if (State is BloodCultListState state)
            Fill(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BloodCultListState list)
            Fill(list);
    }

    private void Fill(BloodCultListState state)
    {
        if (_list == null)
            return;

        _list.RemoveAllChildren();
        foreach (var entry in state.Entries)
        {
            var button = new Button
            {
                Text = entry.Name,
                HorizontalExpand = true,
                ClipText = true,
            };

            var id = entry.Id;
            button.OnPressed += _ =>
            {
                SendMessage(new BloodCultOptionSelectedMessage(id));
                Close();
            };

            _list.AddChild(button);
        }
    }
}

/// <summary>
/// Asks for a name, for a new teleport rune.
/// </summary>
[UsedImplicitly]
public sealed partial class BloodCultNameSelectorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    protected override void Open()
    {
        base.Open();

        var window = this.CreateWindow<FancyWindow>();
        window.Title = Loc.GetString("name-selector-title");

        var lineEdit = new LineEdit { HorizontalExpand = true, MinWidth = 220 };
        var accept = new Button { Text = Loc.GetString("name-selector-accept-button") };

        void Submit()
        {
            SendMessage(new BloodCultNameSelectedMessage(lineEdit.Text));
            Close();
        }

        accept.OnPressed += _ => Submit();
        lineEdit.OnTextEntered += _ => Submit();

        window.ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new(6),
            Children = { lineEdit, accept },
        });

        lineEdit.GrabKeyboardFocus();
    }
}

/// <summary>
/// Runed metal: a radial of cult structures that starts construction placement on the client.
/// </summary>
[UsedImplicitly]
public sealed partial class BloodCultConstructionBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    protected override void Open()
    {
        base.Open();
        IoCManager.InjectDependencies(this);

        if (!EntMan.TryGetComponent<BloodCultConstructionComponent>(Owner, out var construction))
        {
            Close();
            return;
        }

        var constructionSystem = EntMan.System<ConstructionSystem>();
        var options = new List<RadialMenuOptionBase>();
        foreach (var recipeId in construction.Entries)
        {
            if (!_proto.TryIndex(recipeId, out var recipe) ||
                !constructionSystem.TryGetRecipePrototype(recipe.ID, out var target))
                continue;

            options.Add(new RadialMenuActionOption<ConstructionPrototype>(StartPlacement, recipe)
            {
                ToolTip = recipe.Name ?? recipe.ID,
                IconSpecifier = RadialMenuIconSpecifier.With(new EntProtoId(target)),
            });
        }

        var menu = this.CreateWindow<SimpleRadialMenu>();
        menu.Track(Owner);
        menu.SetButtons(options);
        menu.OpenOverMouseScreenPosition();
    }

    private void StartPlacement(ConstructionPrototype recipe)
    {
        var constructionSystem = EntMan.System<ConstructionSystem>();
        if (recipe.Type == ConstructionType.Item)
        {
            constructionSystem.TryStartItemConstruction(recipe.ID);
        }
        else
        {
            _placement.BeginPlacing(new PlacementInformation
                {
                    IsTile = false,
                    PlacementOption = recipe.PlacementMode,
                },
                new ConstructionPlacementHijack(recipe));
        }

        Close();
    }
}
