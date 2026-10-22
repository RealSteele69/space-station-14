using Robust.Shared.Audio;

namespace Content.Server._ClawCommand.AdvancedXenos.Rules;

/// <summary>
/// Claw Command - the xenomorph antagonist: larvae arrive through the station's vents,
/// and Central Command notices "unidentified lifesigns" a while later.
/// Also places the antag spawns for the midround larva event.
/// </summary>
[RegisterComponent, Access(typeof(AdvXenomorphRuleSystem))]
public sealed partial class AdvXenomorphRuleComponent : Component
{
    /// <summary>spawn_announcement_delay = 5000 deciseconds. Null disables the announcement.</summary>
    [DataField]
    public TimeSpan? AnnouncementDelay = TimeSpan.FromSeconds(500);

    [DataField]
    public LocId Announcement = "adv-xeno-rule-announcement";

    [DataField]
    public LocId AnnouncementSender = "adv-xeno-rule-announcement-sender";

    [DataField]
    public SoundSpecifier? AnnouncementSound = new SoundPathSpecifier("/Audio/Announcements/aliens.ogg");

    [DataField]
    public TimeSpan? AnnounceAt;

    /// <summary>
    /// get_vents(): only vents on big pipe networks (more than 50 members), so larvae do not end up in a closet.
    /// Falls back to any open station vent.
    /// </summary>
    [DataField]
    public int MinNetworkSize = 50;
}
