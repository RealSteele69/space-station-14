using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Abilities.Psionics;
using Content.Shared.StatusEffectNew; // Claw Command - old "Muted" status key removed upstream
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Events;

/// <summary>
/// Mutes everyone for a random amount of time.
/// </summary>
internal sealed partial class PsionicCatGotYourTongueRule : StationEventSystem<PsionicCatGotYourTongueRuleComponent>
{
    [Dependency] private MobStateSystem _mobStateSystem = default!;
    [Dependency] private StatusEffectsSystem _statusEffectsSystem = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private SharedAudioSystem _sharedAudioSystem = default!;

    // Claw Command - upstream replaced the "Muted" status key with the StatusEffectMuted entity
    private static readonly EntProtoId MutedEffect = "StatusEffectMuted";


    protected override void Started(EntityUid uid, PsionicCatGotYourTongueRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        List<EntityUid> psionicList = new();

        var query = EntityQueryEnumerator<PsionicComponent, MobStateComponent>();
        while (query.MoveNext(out var psion, out _, out _))
        {
            if (_mobStateSystem.IsAlive(psion) && !HasComp<PsionicInsulationComponent>(psion))
                psionicList.Add(psion);
        }

        foreach (var psion in psionicList)
        {
            var duration = _robustRandom.Next(component.MinDuration, component.MaxDuration);

            // Claw Command - non-refreshing add (stacks onto any existing mute), as the old refresh: false call did
            _statusEffectsSystem.TryAddStatusEffectDuration(psion, MutedEffect, duration);

            _sharedAudioSystem.PlayGlobal(component.Sound, Filter.Entities(psion), false);
        }
    }
}
