using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.AACTablet;

[Serializable, NetSerializable]
public enum AACTabletKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class AACTabletSendPhraseMessage(List<ProtoId<QuickPhrasePrototype>> phraseIds, string prefix) : BoundUserInterfaceMessage
{
    public List<ProtoId<QuickPhrasePrototype>> PhraseIds = phraseIds;
    public string Prefix = prefix;
}

[Serializable, NetSerializable]
public sealed class AACTabletBuiState(HashSet<ProtoId<RadioChannelPrototype>> radioChannels) : BoundUserInterfaceState
{
    public HashSet<ProtoId<RadioChannelPrototype>> RadioChannels = radioChannels;
}
