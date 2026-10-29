// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
//
// Fork note: Content.Goobstation.Maths.FixedPoint.FixedPoint2 -> Content.Shared.FixedPoint.FixedPoint2
// (this fork never took the Goobstation math library, only the vanilla fixed point type exists).
// Also dropped the StatusEffectsSystem indirection for "Feared" (FearedEffect/EntProtoId) - this
// fork's GrantComponentsStatusEffect is a dead data-only stub with no system behind it (verified:
// no EntitySystem anywhere subscribes to it), so SlasherFearSystem instead adds/removes
// SlasherVictimFearBuildupComponent directly. See SlasherFearSystem.Feared.cs.

using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._GoobStation.Slasher.Components;

/// <summary>
/// Drives the Slasher's fear meter.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlasherFearComponent : Component
{
    /// <summary>
    /// Current fear charge.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentMeter;

    [DataField]
    public float MaxMeter = 100f;

    [DataField]
    public float Range = 20f;

    /// <summary>
    /// How often the line-of-sight scan runs.
    /// </summary>
    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(0.3);

    [DataField]
    public float MeterPerJumpscare = 7f;

    /// <summary>
    /// Meter gained per second while at least one victim is in sight.
    /// </summary>
    [DataField]
    public float MeterPassivePerSecond = 1.5f;

    [DataField]
    public float MeterDecayPerSecond = 2f;

    /// <summary>
    /// Grace period after the Slasher last had a victim in sight before the meter starts decaying.
    /// </summary>
    [DataField]
    public TimeSpan MeterGracePeriod = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan JumpscareCooldown = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Immediate fear added to a victim when they are jumpscared.
    /// </summary>
    [DataField]
    public float JumpscareFear = 0.2f;

    /// <summary>
    /// Movement speed bonus granted to the Slasher at full meter.
    /// </summary>
    [DataField]
    public float MaxSpeedBonus = 0.15f;

    /// <summary>
    /// Meter value at which the slasher starts leaving a blood trail.
    /// </summary>
    [DataField]
    public float BloodMeterThreshold = 35f;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "SlasherFear";

    /// <summary>
    /// Whether anyone currently has line of sight on the Slasher.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsObserved;

    [DataField]
    public ProtoId<AlertPrototype> SeenAlert = "SlasherSeen";

    /// <summary>
    /// Whether blood is currently being spilled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive;

    [DataField, AutoNetworkedField]
    public bool IsSpeedBoostActive;

    /// <summary>
    /// Time between blood drops.
    /// </summary>
    [DataField]
    public TimeSpan DropInterval = TimeSpan.FromSeconds(0.2);

    /// <summary>
    /// Amount of blood spilled per drop.
    /// </summary>
    [DataField]
    public FixedPoint2 VolumePerDrop = FixedPoint2.New(1f);

    [DataField, AutoNetworkedField]
    public string BloodTrailReagent = "Blood";

    [DataField, AutoNetworkedField]
    public SoundSpecifier BloodTrailMusic =
        new SoundPathSpecifier("/Audio/_GoobStation/Slasher/Music/slasher_serial_killer_murder_frenzy_insane_horror_soundtrack.ogg")
        {
            Params = AudioParams.Default
                .WithVolume(-2f)
                .WithRolloffFactor(8f)
                .WithMaxDistance(10f)
                .WithLoop(true),
        };

    /// <summary>
    /// How long in seconds the theme takes to fade to silence when the hunt ends.
    /// </summary>
    [DataField]
    public float MusicFadeDuration = 5f;

    /// <summary>
    /// Volume (in dB) treated as silent - the fade drives the theme down to this before stopping it.
    /// </summary>
    [DataField]
    public float MusicSilentVolume = -32f;

    /// <summary>
    /// Components granted to victims while this slasher is frightening them.
    /// Kits can override this to change what being hunted does to a victim.
    /// </summary>
    [DataField]
    public ComponentRegistry GrantedToVictimOnSight = new();

    [DataField, AutoNetworkedField]
    public List<SoundSpecifier> JumpscareSounds = new()
    {
        new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/Jumpscare1.ogg"),
        new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/Jumpscare2.ogg"),
        new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/Jumpscare3.ogg"),
        new SoundPathSpecifier("/Audio/_GoobStation/Effects/Slasher/Jumpscare4.ogg"),
    };

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextCheck;

    /// <summary>
    /// Victims that were in sight on the previous scan, to detect fresh entries.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<NetEntity> Observing = new();

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan NextJumpscare;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    public TimeSpan LastSeenVictim;

    [DataField, AutoNetworkedField]
    public bool IsMusicActive;

    [DataField, AutoNetworkedField]
    public bool IsMusicMuted;

    [ViewVariables]
    public EntityUid? MusicStream;
}
