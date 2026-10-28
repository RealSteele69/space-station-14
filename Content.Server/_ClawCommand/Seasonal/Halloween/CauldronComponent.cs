using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal.Halloween;

/// <summary>
/// Bartholomew's cauldron: feed it junk items and, every few feedings, it crystallizes them into
/// witches' candy.
/// </summary>
[RegisterComponent]
public sealed partial class CauldronComponent : Component
{
    [DataField]
    public int FeedProgress;

    [DataField]
    public int FeedRequired = 3;

    [DataField]
    public EntProtoId Reward = "WitchesCandy";

    [DataField]
    public int RewardMin = 1;

    [DataField]
    public int RewardMax = 2;

    [DataField]
    public SoundSpecifier FeedSound = new SoundPathSpecifier("/Audio/Effects/Chemistry/bubbles.ogg");

    [DataField]
    public SoundSpecifier RewardSound = new SoundPathSpecifier("/Audio/Effects/chime.ogg");
}
