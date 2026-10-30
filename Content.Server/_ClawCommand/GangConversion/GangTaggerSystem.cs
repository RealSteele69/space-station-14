using System.Numerics;
using Content.Server.Decals;
using Content.Server.Popups;
using Content.Shared._ClawCommand.GangConversion.Components;
using Content.Shared.Decals;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Content.Shared.Timing.Systems;
using Robust.Shared.Audio.Systems;
using Content.Shared.Timing.Components;

namespace Content.Server._ClawCommand.GangConversion;

/// <summary>
/// Handles the gang spray tagger: paints turf decals in the wielder's gang colour, erasing a rival's tag
/// underneath first. Standalone system, same shape as the vanilla <c>CrayonSystem</c>.
/// </summary>
public sealed partial class GangTaggerSystem : EntitySystem
{
    [Dependency] private DecalSystem _decals = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GangTaggerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<GangTaggerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnMapInit(Entity<GangTaggerComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<UseDelayComponent>(ent);
    }

    private void OnAfterInteract(Entity<GangTaggerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || ent.Comp.GangId < 0)
            return;

        args.Handled = true;

        if (!args.ClickLocation.IsValid(EntityManager))
        {
            _popup.PopupEntity(Loc.GetString("gang-tagger-invalid-location"), ent, args.User);
            return;
        }

        if (!_useDelay.TryResetDelay(ent.Owner, checkDelayed: true))
        {
            _popup.PopupEntity(Loc.GetString("gang-tagger-recharging"), ent, args.User);
            return;
        }

        var gridUid = _transform.GetGrid(args.ClickLocation);
        if (gridUid is not { } grid)
            return;

        var nearby = _decals.GetDecalsInRange(grid, args.ClickLocation.Position, ent.Comp.ClaimRange,
            d => d.Id == ent.Comp.DecalId);

        var claimedByUs = false;
        foreach (var (decalId, decal) in nearby)
        {
            if (decal.Color is { } color && ColorsMatch(color, ent.Comp.Color))
            {
                claimedByUs = true;
                continue;
            }

            // A rival's tag underneath: scrub it out before we claim the spot.
            _decals.RemoveDecal(grid, decalId);
        }

        if (claimedByUs)
        {
            _popup.PopupEntity(Loc.GetString("gang-tagger-already-tagged"), ent, args.User);
            return;
        }

        if (!_decals.TryAddDecal(ent.Comp.DecalId, args.ClickLocation.Offset(new Vector2(-0.5f, -0.5f)), out _, ent.Comp.Color, cleanable: true))
            return;

        if (ent.Comp.UseSound != null)
            _audio.PlayPvs(ent.Comp.UseSound, ent, ent.Comp.UseSound.Params.WithVariation(0.125f));
    }

    private static bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.01f;
        return MathF.Abs(a.R - b.R) < tolerance &&
               MathF.Abs(a.G - b.G) < tolerance &&
               MathF.Abs(a.B - b.B) < tolerance;
    }
}
