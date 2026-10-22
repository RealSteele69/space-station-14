// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 username <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 whateverusername0 <whateveremail>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aidenkrz <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Heretic.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;

namespace Content.Server.Heretic.Components;

[RegisterComponent, Access(typeof(MansusGraspSystem))]
public sealed partial class MansusGraspComponent : Component
{
    // Claw Command - balance: was 10s.
    [DataField]
    public TimeSpan CooldownAfterUse = TimeSpan.FromSeconds(15);

    [DataField]
    public EntityWhitelist Blacklist = new();

    // Claw Command - balance: was 5s knockdown + 80 stamina, which stamcrit a target off any follow-up hit.
    // A grasp is now an opener that still needs a fight, not a free kill.
    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(3f);

    [DataField]
    public float StaminaDamage = 50f;

    // Claw Command - balance: was 10s; victims get to call for help sooner.
    [DataField]
    public TimeSpan SpeechTime = TimeSpan.FromSeconds(6f);

    [DataField]
    public TimeSpan AffectedTime = TimeSpan.FromMinutes(5);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    [DataField]
    public LocId Invocation = "heretic-speech-mansusgrasp";
}
