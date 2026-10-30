using System.Diagnostics.CodeAnalysis;
using System.Text;
using Content.Shared._ClawCommand.TapeRecorder.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._ClawCommand.TapeRecorder.Systems;

public abstract partial class SharedTapeRecorderSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected const string SlotName = "cassette_tape";

    /// <summary>
    /// Client-side bound user interface type used when the recorder UI is registered at runtime.
    /// </summary>
    private const string RecorderInterfaceType = "TapeRecorderBoundUserInterface";

    /// <summary>
    /// Minimum damage in a single hit that unspools a cassette.
    /// </summary>
    private const float UnspoolDamageThreshold = 5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TapeRecorderComponent, ComponentStartup>(OnRecorderStartup);
        SubscribeLocalEvent<TapeRecorderComponent, ItemSlotEjectAttemptEvent>(OnCassetteRemoveAttempt);
        SubscribeLocalEvent<TapeRecorderComponent, EntRemovedFromContainerMessage>(OnCassetteRemoved);
        SubscribeLocalEvent<TapeRecorderComponent, EntInsertedIntoContainerMessage>(OnCassetteInserted);
        SubscribeLocalEvent<TapeRecorderComponent, ExaminedEvent>(OnRecorderExamined);
        SubscribeLocalEvent<TapeRecorderComponent, ChangeModeTapeRecorderMessage>(OnChangeModeMessage);
        SubscribeLocalEvent<TapeRecorderComponent, EraseTapeRecorderMessage>(OnEraseMessage);

        Subs.BuiEvents<TapeRecorderComponent>(TapeRecorderUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUIOpened);
        });

        SubscribeLocalEvent<TapeCassetteComponent, ExaminedEvent>(OnCassetteExamined);
        SubscribeLocalEvent<TapeCassetteComponent, DamageDealtEvent>(OnDamageDealt);
        SubscribeLocalEvent<TapeCassetteComponent, InteractUsingEvent>(OnInteractingWithCassette);
        SubscribeLocalEvent<TapeCassetteComponent, TapeCassetteRepairDoAfterEvent>(OnTapeCassetteRepair);
    }

    /// <summary>
    /// Process active tape recorder modes
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveTapeRecorderComponent, TapeRecorderComponent>();
        while (query.MoveNext(out var uid, out _, out var comp))
        {
            var ent = (uid, comp);
            if (!TryGetTapeCassette(uid, out var tape))
            {
                SetMode(ent, TapeRecorderMode.Stopped);
                continue;
            }

            var continuing = comp.Mode switch
            {
                TapeRecorderMode.Recording => ProcessRecordingTapeRecorder(ent, frameTime),
                TapeRecorderMode.Playing => ProcessPlayingTapeRecorder(ent, frameTime),
                TapeRecorderMode.Rewinding => ProcessRewindingTapeRecorder(ent, frameTime),
                _ => false
            };

            if (continuing)
                continue;

            SetMode(ent, TapeRecorderMode.Stopped);
            Dirty(tape); // make sure clients have the right value once it's stopped
        }
    }

    /// <summary>
    /// Recorders granted at runtime (e.g. a pAI's voice recorder app) bring no interface of their own,
    /// so register the recorder UI on any entity that has interfaces but lacks this one.
    /// </summary>
    private void OnRecorderStartup(Entity<TapeRecorderComponent> ent, ref ComponentStartup args)
    {
        if (!_net.IsServer)
            return;

        if (!TryComp<UserInterfaceComponent>(ent, out var ui) || _ui.HasUi(ent, TapeRecorderUIKey.Key, ui))
            return;

        _ui.SetUi((ent, ui), TapeRecorderUIKey.Key, new InterfaceData(RecorderInterfaceType, requireInputValidation: false));
    }

    private void OnUIOpened(Entity<TapeRecorderComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent);
    }

    /// <summary>
    /// UI message to wipe the tape clean, only offered by recorders that allow it.
    /// </summary>
    private void OnEraseMessage(Entity<TapeRecorderComponent> ent, ref EraseTapeRecorderMessage args)
    {
        if (!ent.Comp.CanErase || HasComp<ActiveTapeRecorderComponent>(ent))
            return;

        if (!TryGetTapeCassette(ent, out var tape))
            return;

        tape.Comp.RecordedData.Clear();
        tape.Comp.Buffer.Clear();
        tape.Comp.CurrentPosition = 0f;
        Dirty(tape);

        Audio.PlayPredicted(ent.Comp.RewindSound, ent, args.Actor);
        UpdateUI(ent);
    }

    /// <summary>
    /// UI message when choosing between recorder modes
    /// </summary>
    private void OnChangeModeMessage(Entity<TapeRecorderComponent> ent, ref ChangeModeTapeRecorderMessage args)
    {
        SetMode(ent, args.Mode, args.Actor);
    }

    /// <summary>
    /// Update the tape position and overwrite any messages between the previous and new position
    /// </summary>
    /// <returns>True if the tape recorder should continue in the current mode, False if it should switch to the Stopped mode</returns>
    private bool ProcessRecordingTapeRecorder(Entity<TapeRecorderComponent> ent, float frameTime)
    {
        if (!TryGetTapeCassette(ent, out var tape))
            return false;

        var currentTime = tape.Comp.CurrentPosition + frameTime;

        // Remove any old messages in the segment we just recorded over
        tape.Comp.RecordedData.RemoveAll(x => x.Timestamp > tape.Comp.CurrentPosition && x.Timestamp <= currentTime);

        tape.Comp.RecordedData.AddRange(tape.Comp.Buffer);

        tape.Comp.Buffer.Clear();

        // Update the tape's current time
        tape.Comp.CurrentPosition = (float) Math.Min(currentTime, tape.Comp.MaxCapacity.TotalSeconds);

        // If we have reached the end of the tape - stop
        return tape.Comp.CurrentPosition < tape.Comp.MaxCapacity.TotalSeconds;
    }

    /// <summary>
    /// Update the tape position and play any messages with timestamps between the previous and new position
    /// </summary>
    /// <returns>True if the tape recorder should continue in the current mode, False if it should switch to the Stopped mode</returns>
    private bool ProcessPlayingTapeRecorder(Entity<TapeRecorderComponent> ent, float frameTime)
    {
        if (!TryGetTapeCassette(ent, out var tape))
            return false;

        // Get the segment of the tape to be played and any messages within that time period
        var currentTime = tape.Comp.CurrentPosition + frameTime;

        ReplayMessagesInSegment(ent, tape.Comp, tape.Comp.CurrentPosition, currentTime);

        // Update the tape's position
        tape.Comp.CurrentPosition = (float) Math.Min(currentTime, tape.Comp.MaxCapacity.TotalSeconds);

        // Stop when we reach the end of the tape
        return tape.Comp.CurrentPosition < tape.Comp.MaxCapacity.TotalSeconds;
    }

    /// <summary>
    /// Update the tape position in reverse
    /// </summary>
    /// <returns>True if the tape recorder should continue in the current mode, False if it should switch to the Stopped mode</returns>
    private bool ProcessRewindingTapeRecorder(Entity<TapeRecorderComponent> ent, float frameTime)
    {
        if (!TryGetTapeCassette(ent, out var tape))
            return false;

        // Calculate how far we have rewound
        var rewindTime = frameTime * ent.Comp.RewindSpeed;
        // Update the current time, clamp to 0
        tape.Comp.CurrentPosition = Math.Max(0, tape.Comp.CurrentPosition - rewindTime);

        // If we have reached the beginning of the tape, stop
        return tape.Comp.CurrentPosition >= float.Epsilon;
    }

    /// <summary>
    /// Plays messages back on the server.
    /// Does nothing on the client.
    /// </summary>
    protected virtual void ReplayMessagesInSegment(Entity<TapeRecorderComponent> ent, TapeCassetteComponent tape, float segmentStart, float segmentEnd)
    {
    }

    /// <summary>
    /// Start repairing a damaged tape when using a screwdriver or pen on it
    /// </summary>
    protected void OnInteractingWithCassette(Entity<TapeCassetteComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Is the tape damaged?
        if (!IsUnspooled(ent))
            return;

        // Are we using a valid repair tool?
        if (_whitelist.IsWhitelistFail(ent.Comp.RepairWhitelist, args.Used))
            return;

        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.RepairDelay, new TapeCassetteRepairDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
            NeedHand = true
        });

        if (!started)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cassette-repair-start", ("item", ent.Owner)), args.User, args.User);
    }

    /// <summary>
    /// Repair a damaged tape
    /// </summary>
    protected void OnTapeCassetteRepair(Entity<TapeCassetteComponent> ent, ref TapeCassetteRepairDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        // Cant repair if not damaged
        if (!IsUnspooled(ent))
            return;

        _appearance.SetData(ent, ToggleableVisuals.Enabled, false);
        AddComp<FitsInTapeRecorderComponent>(ent);
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cassette-repair-finish", ("item", ent.Owner)), args.User, args.User);
    }

    /// <summary>
    /// When the cassette has been damaged, corrupt an entry and unspool it
    /// </summary>
    protected void OnDamageDealt(Entity<TapeCassetteComponent> ent, ref DamageDealtEvent args)
    {
        // Tapes built into a recorder are protected by it.
        if (HasComp<TapeRecorderComponent>(ent) || args.Damage.GetTotal() < UnspoolDamageThreshold)
            return;

        _appearance.SetData(ent, ToggleableVisuals.Enabled, true);

        RemComp<FitsInTapeRecorderComponent>(ent);
        CorruptRandomEntry(ent);
    }

    private void OnCassetteExamined(Entity<TapeCassetteComponent> ent, ref ExaminedEvent args)
    {
        // A tape built into a recorder is described by the recorder itself.
        if (HasComp<TapeRecorderComponent>(ent))
            return;

        OnTapeExamined(ent, ref args);
    }

    protected void OnTapeExamined(Entity<TapeCassetteComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (IsUnspooled(ent))
        {
            args.PushMarkup(Loc.GetString("tape-cassette-damaged"));
            return;
        }

        var positionPercentage = Math.Floor(ent.Comp.CurrentPosition / ent.Comp.MaxCapacity.TotalSeconds * 100);
        var tapePosMsg = Loc.GetString("tape-cassette-position", ("position", positionPercentage));
        args.PushMarkup(tapePosMsg);
    }

    protected void OnRecorderExamined(Entity<TapeRecorderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        // Check if we have a tape cassette inserted
        if (!TryGetTapeCassette(ent, out var tape))
        {
            args.PushMarkup(Loc.GetString("tape-recorder-empty"));
            return;
        }

        var state = ent.Comp.Mode.ToString().ToLower();
        args.PushMarkup(Loc.GetString("tape-recorder-" + state));

        OnTapeExamined(tape, ref args);
    }

    /// <summary>
    /// Prevent removing the tape cassette while the recorder is active
    /// </summary>
    protected void OnCassetteRemoveAttempt(Entity<TapeRecorderComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (!HasComp<ActiveTapeRecorderComponent>(ent))
            return;

        args.Cancelled = true;
    }

    protected void OnCassetteRemoved(Entity<TapeRecorderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != SlotName)
            return;

        SetMode(ent, TapeRecorderMode.Stopped);
        UpdateAppearance(ent);
        UpdateUI(ent);
    }

    protected void OnCassetteInserted(Entity<TapeRecorderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != SlotName)
            return;

        UpdateAppearance(ent);
        UpdateUI(ent);
    }

    /// <summary>
    /// Update the appearance of the tape recorder.
    /// </summary>
    protected void UpdateAppearance(Entity<TapeRecorderComponent> ent)
    {
        var hasCassette = TryGetTapeCassette(ent, out _);
        _appearance.SetData(ent, TapeRecorderVisuals.Mode, ent.Comp.Mode);
        _appearance.SetData(ent, TapeRecorderVisuals.TapeInserted, hasCassette);
    }

    /// <summary>
    /// Choose a random recorded entry on the cassette and replace some of the text with hashes
    /// </summary>
    protected void CorruptRandomEntry(TapeCassetteComponent tape)
    {
        if (tape.RecordedData.Count == 0)
            return;

        var entry = _random.Pick(tape.RecordedData);

        var corruption = Loc.GetString("tape-recorder-message-corruption");

        var corruptedMessage = new StringBuilder();
        foreach (var character in entry.Message)
        {
            if (_random.Prob(tape.CorruptionChance))
                corruptedMessage.Append(corruption);
            else
                corruptedMessage.Append(character);
        }

        entry.Name = Loc.GetString("tape-recorder-voice-unintelligible");
        entry.Message = corruptedMessage.ToString();
    }

    /// <summary>
    /// Set the tape recorder mode and dirty if it is different from the previous mode
    /// </summary>
    /// <param name="user">The entity that requested the change, if any. Used to predict the switch sound.</param>
    private void SetMode(Entity<TapeRecorderComponent> ent, TapeRecorderMode mode, EntityUid? user = null)
    {
        if (mode == ent.Comp.Mode)
            return;

        if (mode == TapeRecorderMode.Stopped)
        {
            RemComp<ActiveTapeRecorderComponent>(ent);
        }
        else
        {
            // can't play without a tape in it...
            if (!TryGetTapeCassette(ent, out _))
                return;

            EnsureComp<ActiveTapeRecorderComponent>(ent);
        }

        var sound = mode switch
        {
            TapeRecorderMode.Stopped => ent.Comp.StopSound,
            TapeRecorderMode.Rewinding => ent.Comp.RewindSound,
            _ => ent.Comp.PlaySound
        };

        // A user-driven switch is predicted on their client; automatic switches (end of tape, ejection)
        // are only voiced by the server so nobody hears the click twice.
        if (user != null)
            Audio.PlayPredicted(sound, ent, user);
        else if (_net.IsServer)
            Audio.PlayPvs(sound, ent);

        ent.Comp.Mode = mode;
        Dirty(ent);

        UpdateAppearance(ent);
        UpdateUI(ent);
    }

    /// <summary>
    /// Whether a cassette has been unspooled by damage and needs repairing before it fits a recorder again.
    /// Tapes built into a recorder never unspool.
    /// </summary>
    private bool IsUnspooled(EntityUid tape)
    {
        return !HasComp<FitsInTapeRecorderComponent>(tape) && !HasComp<TapeRecorderComponent>(tape);
    }

    /// <summary>
    /// Gets the tape this recorder operates on: a tape built into the recorder itself, or the cassette in its slot.
    /// </summary>
    protected bool TryGetTapeCassette(EntityUid ent, [NotNullWhen(true)] out Entity<TapeCassetteComponent> tape)
    {
        if (TryComp<TapeCassetteComponent>(ent, out var integrated))
        {
            tape = new(ent, integrated);
            return true;
        }

        if (_slots.GetItemOrNull(ent, SlotName) is not { } cassette)
        {
            tape = default!;
            return false;
        }

        if (!TryComp<TapeCassetteComponent>(cassette, out var comp))
        {
            tape = default!;
            return false;
        }

        tape = new(cassette, comp);
        return true;
    }

    private void UpdateUI(Entity<TapeRecorderComponent> ent)
    {
        var (uid, comp) = ent;
        if (!_ui.IsUiOpen(uid, TapeRecorderUIKey.Key))
            return;

        var hasCassette = TryGetTapeCassette(ent, out var tape);
        var hasData = false;
        var currentTime = 0f;
        var maxTime = 0f;
        var cassetteName = Loc.GetString("tape-recorder-unnamed-tape-name");
        var cooldown = comp.PrintCooldown;

        if (hasCassette)
        {
            hasData = tape.Comp.RecordedData.Count > 0;
            currentTime = tape.Comp.CurrentPosition;
            maxTime = (float) tape.Comp.MaxCapacity.TotalSeconds;

            if (tape.Owner == uid)
                cassetteName = Loc.GetString(comp.IntegratedTapeName);
            else if (TryComp<LabelComponent>(tape, out var labelComp) && labelComp.CurrentLabel != null)
                cassetteName = labelComp.CurrentLabel;
        }

        var state = new TapeRecorderState(
            hasCassette,
            hasData,
            currentTime,
            maxTime,
            cassetteName,
            cooldown,
            comp.CanPrint,
            comp.CanErase);

        _ui.SetUiState(uid, TapeRecorderUIKey.Key, state);
    }
}

[Serializable, NetSerializable]
public sealed partial class TapeCassetteRepairDoAfterEvent : SimpleDoAfterEvent;
