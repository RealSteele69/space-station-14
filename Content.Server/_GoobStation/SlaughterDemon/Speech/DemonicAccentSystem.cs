// SPDX-FileCopyrightText: 2024 Goobstation Contributors
// SPDX-FileCopyrightText: 2025 ClawCommand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later
// Ported from Goobstation for ClawCommand.
// Originally Content.Goobstation.Server.Speech.DemonicAccentSystem. Adapted: the replacement accent id is
// "slaughterDemon" (matching the ported Accents/slaughter_demon.yml prototype id) rather than source's
// "slaughter_demon" string, which didn't actually match its own accent prototype's id there either.

using Content.Shared._GoobStation.SlaughterDemon.Other;
using Content.Shared.Speech;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._GoobStation.SlaughterDemon.Speech;

public sealed partial class DemonicAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemonicAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<DemonicAccentComponent> entity, ref AccentGetEvent args)
    {
        var message = args.Message;

        message = _replacement.ApplyReplacements(message, "slaughterDemon");

        if (_random.Prob(0.15f))
        {
            var pick = _random.Next(1, 8);
            message = message + ' ' + Loc.GetString($"accent-demonic-suffix-{pick}");
        }

        message = message.ToUpperInvariant();

        args.Message = message;
    }
}
