// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class MedievalAccentSystem : RelayAccentSystem<MedievalAccentComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override string Accentuate(string message, Entity<MedievalAccentComponent>? ent = null)
    {
        var msg = _replacement.ApplyReplacements(message, "medieval");
        if (msg.Length == 0)
            return msg;

        if (ent is { } accent && _random.Prob(accent.Comp.PrefixChance))
        {
            var pick = _random.Next(1, accent.Comp.PrefixCount + 1);
            msg = Loc.GetString($"accent-medieval-prefix-{pick}") + " " + char.ToLower(msg[0]) + msg[1..];
        }

        return char.ToUpper(msg[0]) + msg[1..];
    }
}
