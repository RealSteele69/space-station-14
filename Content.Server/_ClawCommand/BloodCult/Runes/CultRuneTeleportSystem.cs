using Content.Shared._ClawCommand.BloodCult;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Server.GameObjects;

namespace Content.Server._ClawCommand.BloodCult.Runes;

/// <summary>
/// Teleport runes: named on scribing, and send whoever stands on them to another teleport rune.
/// </summary>
public sealed partial class CultRuneTeleportSystem : EntitySystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private CultRuneBaseSystem _cultRune = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const int MaxNameLength = 32;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultRuneTeleportComponent, AfterRunePlacedEvent>(OnPlaced);
        SubscribeLocalEvent<CultRuneTeleportComponent, BloodCultNameSelectedMessage>(OnNameSelected);
        SubscribeLocalEvent<CultRuneTeleportComponent, TryInvokeCultRuneEvent>(OnInvoked);
        SubscribeLocalEvent<CultRuneTeleportComponent, BloodCultOptionSelectedMessage>(OnDestinationSelected);
    }

    private void OnPlaced(Entity<CultRuneTeleportComponent> rune, ref AfterRunePlacedEvent args)
    {
        _ui.OpenUi(rune.Owner, BloodCultUiKey.RuneName, args.User);
    }

    private void OnNameSelected(Entity<CultRuneTeleportComponent> rune, ref BloodCultNameSelectedMessage args)
    {
        var name = args.Name.Trim();
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        rune.Comp.Name = name;
        _ui.CloseUi(rune.Owner, BloodCultUiKey.RuneName);
    }

    private void OnInvoked(Entity<CultRuneTeleportComponent> rune, ref TryInvokeCultRuneEvent args)
    {
        if (_ui.IsUiOpen(rune.Owner, BloodCultUiKey.RuneTeleport) || !TryGetTeleportRunes(args.User, out var runes, rune))
        {
            args.Cancel();
            return;
        }

        _ui.SetUiState(rune.Owner, BloodCultUiKey.RuneTeleport, new BloodCultListState(runes));
        _ui.OpenUi(rune.Owner, BloodCultUiKey.RuneTeleport, args.User);
    }

    private void OnDestinationSelected(Entity<CultRuneTeleportComponent> origin, ref BloodCultOptionSelectedMessage args)
    {
        if (!args.UiKey.Equals(BloodCultUiKey.RuneTeleport))
            return;

        _ui.CloseUi(origin.Owner, BloodCultUiKey.RuneTeleport);

        if (!NetEntity.TryParse(args.Id, out var netDestination) || !TryGetEntity(netDestination, out var destination) ||
            !HasComp<CultRuneTeleportComponent>(destination))
            return;

        _audio.PlayPvs(origin.Comp.TeleportOutSound, origin);
        foreach (var target in _cultRune.GetTargetsNearRune(origin, origin.Comp.TeleportGatherRange))
        {
            _cultRune.TeleportTo(target, destination.Value);
        }
    }

    /// <summary>
    /// Every teleport rune except <paramref name="exclude"/>, as list entries.
    /// </summary>
    public bool TryGetTeleportRunes(EntityUid user, out List<BloodCultListEntry> runes, EntityUid? exclude = null)
    {
        runes = new List<BloodCultListEntry>();
        var query = EntityQueryEnumerator<CultRuneTeleportComponent>();
        while (query.MoveNext(out var uid, out var teleport))
        {
            if (uid == exclude)
                continue;

            var name = string.IsNullOrWhiteSpace(teleport.Name) ? Loc.GetString("cult-teleport-unnamed") : teleport.Name;
            runes.Add(new BloodCultListEntry(GetNetEntity(uid).ToString(), name));
        }

        if (runes.Count != 0)
            return true;

        _popup.PopupEntity(Loc.GetString("cult-teleport-not-found"), user, user);
        return false;
    }
}
