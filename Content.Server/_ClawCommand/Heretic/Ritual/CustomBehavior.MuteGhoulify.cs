// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 username <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 whateverusername0 <whateveremail>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitcode.Heretic.Components;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Prototypes;
using Content.Shared.StatusEffectNew; // Claw Command - MutedComponent replaced by entity status effects
using Robust.Shared.Prototypes;

namespace Content.Server.Heretic.Ritual;

public sealed partial class RitualMuteGhoulifyBehavior : RitualSacrificeBehavior
{
    // Claw Command - permanent, non-rejuvenate-removable mute (the old MutedComponent was not cleared by rejuvenate)
    private static readonly EntProtoId MutedEffect = "TraitStatusEffectMuted";

    public override void Finalize(RitualData args)
    {
        if (args is { Limit: > 0, Limited: not null } && args.Limited.Count >= args.Limit)
            return;

        var statusEffects = args.EntityManager.System<StatusEffectsSystem>(); // Claw Command

        for (var i = 0; i < Math.Min(uids.Count, Max); i++)
        {
            var uid = uids[i];

            var minion = args.EntityManager.EnsureComponent<HereticMinionComponent>(uid);
            minion.BoundHeretic = args.Performer;

            var ghoul = new GhoulComponent
            {
                TotalHealth = 100f,
                GiveBlade = true,
            };
            args.EntityManager.AddComponent(uid, ghoul, overwrite: true);
            // Claw Command - MutedComponent was replaced by status effect entities; MutedStatusEffectComponent on the mob
            // itself does nothing. Apply a permanent (null duration) mute that rejuvenate does not clear, like the old component.
            statusEffects.TrySetStatusEffectDuration(uid, MutedEffect);
            args.EntityManager.EnsureComponent<HereticBladeUserBonusDamageComponent>(uid);

            if (args.Limited == null)
                continue;

            args.Limited.Add(uid);

            if (args.Limit > 0 && args.Limited.Count >= args.Limit)
                break;
        }
    }
}
