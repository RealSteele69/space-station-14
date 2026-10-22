using Content.Shared._ClawCommand.Vehicle;
using Content.Shared.Movement.Components;
using Content.Shared.Vehicle.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Client._ClawCommand.Vehicle;

/// <summary>
/// Claw Command - Plays a vehicle's engine while someone's at the wheel and it can run. The loop is layered at
/// a few pitches (<see cref="VehicleHandlingComponent.EnginePitches"/>) and crossfaded by speed, so it revs up
/// as the vehicle picks up speed. Purely local: every client hears every running engine nearby.
/// </summary>
public sealed partial class VehicleEngineSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private VehicleSystem _vehicle = default!;

    private readonly Dictionary<EntityUid, List<EntityUid>> _engines = new();
    private readonly List<EntityUid> _stopped = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleHandlingComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var vehicle in new List<EntityUid>(_engines.Keys))
            StopEngine(vehicle);
    }

    private void OnShutdown(Entity<VehicleHandlingComponent> ent, ref ComponentShutdown args)
    {
        StopEngine(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<VehicleHandlingComponent, InputMoverComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var handling, out var mover, out var physics))
        {
            var running = handling.EngineSound != null
                          && handling.EnginePitches.Count > 0
                          && mover.CanMove
                          && _vehicle.HasOperator(uid);

            if (!running)
            {
                StopEngine(uid);
                continue;
            }

            if (!_engines.TryGetValue(uid, out var layers) || layers.Count != handling.EnginePitches.Count || !layers.TrueForAll(Exists))
            {
                StopEngine(uid);
                layers = new List<EntityUid>();
                foreach (var pitch in handling.EnginePitches)
                {
                    var audioParams = AudioParams.Default.WithLoop(true).WithVolume(handling.EngineVolume).WithPitchScale(pitch);
                    if (_audio.PlayEntity(handling.EngineSound, Filter.Local(), uid, false, audioParams) is { } playing)
                        layers.Add(playing.Entity);
                }

                if (layers.Count != handling.EnginePitches.Count)
                {
                    foreach (var layer in layers)
                        _audio.Stop(layer);
                    continue;
                }

                _engines[uid] = layers;
            }

            var mods = CompOrNull<MovementSpeedModifierComponent>(uid);
            var top = (mods?.CurrentSprintSpeed ?? MovementSpeedModifierComponent.DefaultBaseSprintSpeed) * handling.TopSpeedMultiplier;
            var revs = top > 0f ? Math.Clamp(physics.LinearVelocity.Length() / top, 0f, 1f) : 0f;

            // Each layer is loudest at its own spot along the rev range and fades out towards its neighbours.
            var baseGain = MathF.Pow(10f, handling.EngineVolume / 20f);
            var position = revs * (layers.Count - 1);
            for (var i = 0; i < layers.Count; i++)
            {
                var weight = MathF.Max(0f, 1f - MathF.Abs(position - i));
                _audio.SetGain(layers[i], baseGain * weight);
            }
        }

        // Engines whose vehicle went out of view.
        foreach (var vehicle in _engines.Keys)
        {
            if (!Exists(vehicle))
                _stopped.Add(vehicle);
        }

        foreach (var vehicle in _stopped)
            StopEngine(vehicle);
        _stopped.Clear();
    }

    private void StopEngine(EntityUid vehicle)
    {
        if (!_engines.Remove(vehicle, out var layers))
            return;

        foreach (var layer in layers)
            _audio.Stop(layer);
    }
}
