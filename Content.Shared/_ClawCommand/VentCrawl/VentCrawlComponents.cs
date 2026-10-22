// Claw Command - vent crawling. The behaviour follows the original DM source; the implementation is new.

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._ClawCommand.VentCrawl;

/// <summary>
/// This mob can climb into air vents and scrubbers and crawl through the connected pipes.
/// Alt-click (or right-click) an open vent or scrubber next to you to climb in; movement keys follow the pipes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VentCrawlerComponent : Component
{
    /// <summary>do_after(src, 45) before you are in.</summary>
    [DataField]
    public TimeSpan EnterTime = TimeSpan.FromSeconds(4.5);

    /// <summary>
    /// ventcrawl_carry(): most mobs must carry nothing, held or worn. Xeno larvae skip the check.
    /// Being cuffed always stops you.
    /// </summary>
    [DataField]
    public bool AllowHeldItems;

    /// <summary>Slowest and fastest time per pipe segment; the mob's own speed picks a value between.</summary>
    [DataField]
    public float MinMoveDelay = 0.12f;

    [DataField]
    public float MaxMoveDelay = 0.45f;

    /// <summary>sound/machines/ventcrawl.ogg, played at the pipe every few seconds while moving.</summary>
    [DataField]
    public SoundSpecifier? CrawlSound = new SoundPathSpecifier("/Audio/_ClawCommand/VentCrawl/ventcrawl.ogg",
        AudioParams.Default.WithVolume(-4f).WithMaxDistance(5f).WithVariation(0.1f));

    [DataField]
    public TimeSpan CrawlSoundCooldown = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Present while a mob is inside the ducts. The mob itself sits in a container on an invisible duct entity
/// that is moved from pipe to pipe.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class VentCrawlingComponent : Component
{
    /// <summary>The pipe, vent or other atmos device the crawler is currently in.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Pipe;

    [DataField, AutoNetworkedField]
    public EntityUid? Duct;

    [DataField, AutoPausedField]
    public TimeSpan NextMove;

    [DataField, AutoPausedField]
    public TimeSpan NextSound;

    /// <summary>Eye.DrawFov before going in; restored on the way out.</summary>
    [DataField]
    public bool PreviousDrawFov = true;
}

/// <summary>The invisible carrier that holds a crawler while it is in the pipes.</summary>
[RegisterComponent]
public sealed partial class VentCrawlDuctComponent : Component
{
    public const string ContainerId = "vent_crawl_duct";

    [DataField]
    public EntityUid? Crawler;
}

[Serializable, NetSerializable]
public sealed partial class VentCrawlEnterDoAfterEvent : SimpleDoAfterEvent;
