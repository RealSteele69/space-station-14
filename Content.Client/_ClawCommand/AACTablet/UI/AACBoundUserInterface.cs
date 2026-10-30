using Content.Shared._ClawCommand.AACTablet;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._ClawCommand.AACTablet.UI;

public sealed class AACBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private AACWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AACWindow>();
        _window.PhraseButtonPressed += OnPhraseButtonPressed;
    }

    private void OnPhraseButtonPressed(List<ProtoId<QuickPhrasePrototype>> phraseIds, string prefix)
    {
        // Copy the list, the window reuses its buffers.
        SendMessage(new AACTabletSendPhraseMessage(new List<ProtoId<QuickPhrasePrototype>>(phraseIds), prefix));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not AACTabletBuiState msg)
            return;

        _window?.Update(msg);
    }
}
