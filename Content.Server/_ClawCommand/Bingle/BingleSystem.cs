// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._ClawCommand.Bingle;
using Content.Shared.CombatMode;
using Content.Shared.Interaction.Events;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._ClawCommand.Bingle;

public sealed partial class BingleSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private static readonly ProtoId<PolymorphPrototype> UpgradePolymorph = "BinglePolymorph";
    private static readonly EntProtoId PitPrototype = "BinglePit";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BingleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BingleComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<BingleComponent, ToggleCombatActionEvent>(OnCombatToggle, after: [typeof(SharedCombatModeSystem)]);
    }

    private void OnMapInit(Entity<BingleComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (xform.MapID == MapId.Nullspace || xform.GridUid == null)
            return;

        if (ent.Comp.Prime)
        {
            ent.Comp.MyPit = Spawn(PitPrototype, xform.Coordinates);
            return;
        }

        // Regular bingles spawn on top of the pit that made them.
        foreach (var pit in _lookup.GetEntitiesInRange<BinglePitComponent>(xform.Coordinates, 0.75f))
        {
            ent.Comp.MyPit = pit;

            // Bingles born from a pit that already levelled up can upgrade straight away.
            if (pit.Comp.Level > 1)
                UpgradeBingle(ent);
            break;
        }
    }

    /// <summary>
    /// Run by the pit when it levels up. Grants the polymorph into the upgraded bingle once.
    /// </summary>
    public void UpgradeBingle(Entity<BingleComponent> ent)
    {
        if (ent.Comp.Upgraded)
            return;

        var polyComp = EnsureComp<PolymorphableComponent>(ent);
        _polymorph.CreatePolymorphAction(UpgradePolymorph, (ent, polyComp));

        _popup.PopupEntity(Loc.GetString("bingle-upgrade-success"), ent, ent);
        ent.Comp.Upgraded = true;
    }

    private void OnAttackAttempt(Entity<BingleComponent> ent, ref AttackAttemptEvent args)
    {
        // No friendly fire on other bingles or the pit.
        if (HasComp<BinglePitComponent>(args.Target) || HasComp<BingleComponent>(args.Target))
            args.Cancel();
    }

    private void OnCombatToggle(Entity<BingleComponent> ent, ref ToggleCombatActionEvent args)
    {
        if (!TryComp<CombatModeComponent>(ent, out var combat))
            return;

        _appearance.SetData(ent, BingleVisual.Combat, combat.IsInCombatMode);
    }
}
