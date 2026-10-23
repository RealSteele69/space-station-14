using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Seasonal;

/// <summary>
/// A festive cracker that takes two people to pull. Use it on someone with an empty hand:
/// it pops, each of you keeps half, a terrible joke falls out and the other person gets a paper hat.
/// </summary>
[RegisterComponent, Access(typeof(ChristmasCrackerSystem))]
public sealed partial class ChristmasCrackerComponent : Component
{
    /// <summary>
    /// Left in the puller's hand.
    /// </summary>
    [DataField]
    public EntProtoId UserHalf = "ChristmasCrackerHalfLeft";

    /// <summary>
    /// Put in the other person's empty hand.
    /// </summary>
    [DataField]
    public EntProtoId TargetHalf = "ChristmasCrackerHalfRight";

    [DataField]
    public EntProtoId Joke = "PaperCrackerJoke";

    [DataField]
    public EntProtoId Hat = "ClothingHeadHatXmasCrown";

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/snap.ogg");
}
