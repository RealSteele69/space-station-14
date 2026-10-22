## Game rule

blood-cult-title = The Blood Cult
blood-cult-description = The oldest and strongest emotion of mankind is fear, and the oldest and strongest kind of fear is fear of the unknown.
blood-cult-roundend-name = blood cultist

roles-antag-blood-cultist-name = Blood Cultist
roles-antag-blood-cultist-objective = Sacrifice the chosen one and summon the Geometer of Blood, Nar'Sie.
role-subtype-blood-cultist = Blood Cultist

blood-cult-role-greeting =
    The Geometer of Blood, Nar'Sie, has sent a number of her followers to this station.
    As a cultist you have an abundance of cult magic at your disposal, something for every situation.
    Work with your brethren to summon an avatar of your eldritch goddess!
blood-cult-role-converted =
    Your mind floods with visions of a vast, blood-red geometry. You see the truth now.
    You serve the Geometer of Blood, Nar'Sie. Obey the cult and help bring her into this world!
blood-cult-role-briefing-short = Speak to your brethren through the cult's mind with :g. Use your ritual dagger to draw runes, and the Blood Magic action to prepare spells.
blood-cult-role-briefing-target = Nar'Sie demands the offering of {$name}.
blood-cult-role-briefing-rending-locations = The veil is thin {$location}.
blood-cult-role-briefing-emergency-rending = The veil is thin everywhere. We can draw {$amount} more final runes anywhere on the station.

blood-cult-stage-red-eyes = The cult grows strong. Our eyes now burn red with Nar'Sie's power; we can no longer hide among the crew.
blood-cult-stage-halos = The cult is legion. Halos of blood crown us all, and the Geometer has chosen a leader.
blood-cult-leader-chosen = {$name} has been chosen to lead the cult.
blood-cult-deconverted = The whispers fall silent. Whatever held your mind is gone, and you remember little of it.
blood-cult-purifying = Holy water burns through your veins, washing out the blood pact!

blood-cult-condition-win = The Geometer of Blood has been summoned! The cult is victorious.
blood-cult-condition-draw = The cult endured, but Nar'Sie was never summoned.
blood-cult-condition-failure = The crew stopped the rending of reality. Every cultist fell.
blood-cult-summary-converted = The cult converted {$count} {$count ->
    [one] soul
   *[other] souls
} to its cause.
blood-cult-summary-sacrificed = The cult offered {$count} {$count ->
    [one] body
   *[other] bodies
} to Nar'Sie.
blood-cult-summary-target-dead = The chosen offering, {$name}, was slain.
blood-cult-summary-target-alive = The chosen offering, {$name}, survived.

blood-cultists-list-start = The followers of the Geometer of Blood were:
blood-cultists-list-name = [color=White]{$name}[/color] ([color=gray]{$user}[/color])
blood-cultists-list-name-converted = [color=White]{$name}[/color] [color=gray](converted)[/color]

objective-issuer-blood-cult = [color=#b51b1b]The Geometer of Blood[/color]
objective-condition-blood-cult-sacrifice-title = Offer {$targetName}, {CAPITALIZE($job)} to Nar'Sie

admin-verb-text-make-blood-cultist = Make Blood Cultist
admin-verb-make-blood-cultist = Make the target into a blood cultist.

chat-radio-blood-cult = Blood Cult

guide-entry-blood-cult = Blood Cult

alerts-blood-cult-empowered-name = Empowered
alerts-blood-cult-empowered-desc = Blood magic and rune scribing take far less time and cost far less blood, and you can prepare more spells. Stay near cult floor to keep it.

reagent-effect-guidebook-purify-blood-cult =
    { $chance ->
        [1] Purges
        *[other] purge
    } the Blood Cult's pact from a cultist over time

## Blood magic

blood-cult-spells-prepare = Prepare: {$spell}
blood-cult-spells-dispel = Dispel: {$spell}
blood-cult-spells-too-many = You can't hold any more blood spells.
blood-cult-spells-no-empty-hand = You need a free hand to cast that.
blood-cult-spells-muted = You can't chant the incantation while muted!
blood-cult-spells-protected = Something shields them from your magic.
blood-cult-twisted-construction-invalid = That can't be twisted.
blood-cult-list-title = Choose

## Items

cult-item-component-generic = The item refuses to obey your will!
cult-item-component-attack-fail = The weapon refuses to obey your will!
cult-item-component-equip-fail = The armor refuses to obey your will!
cult-item-component-throw-fail = The weapon refuses to obey your will!
cult-item-component-block-fail = The shield betrays you!
cult-runed-door-pry-fail = The runes on the door flare and push you back!

blood-rites-stored-blood = It has [color=darkred]{$amount}u of blood[/color] stored.
blood-rites-not-enough-blood = You don't have enough blood stored.
blood-rites-heal-dead = Only a rune of rejuvenation can bring back the dead.
blood-rites-no-blood-left = You use the last drop of blood stored in your rites.
blood-rites-craft-entry = {$name} ({$cost}u of blood, {$stored}u stored)

soul-shard-slot-name = Soul shard
soul-shard-try-insert-no-soul = The shard has no soul.
soul-shard-blessed = The shard glows with a soft, holy light.

ghost-role-information-soul-shard-name = Soul Shard
ghost-role-information-soul-shard-description = Become a servant of the Blood Cult.
ghost-role-information-soul-shard-rules = You are bound to the Blood Cult. Take the form of a construct and help your masters bring their Goddess into the world!
ghost-role-information-soul-shard-holy-name = Blessed Soul Shard
ghost-role-information-soul-shard-holy-description = Become a purified spirit and help the crew.
ghost-role-information-soul-shard-holy-rules = You are free of Nar'Sie. Take the form of a purified construct and help the crew stop the Blood Cult!

shuttle-curse-cant-activate = Nar'Sie's power doesn't reach here.
shuttle-curse-max-charges = You try to shatter the orb, but it stays solid as rock!
shuttle-curse-shuttle-arrived = The shuttle has already arrived! You can't delay it anymore.
shuttle-curse-shuttle-not-called = The shuttle hasn't been called yet.
shuttle-curse-success = You shatter the orb! A dark mist billows out, reaching for the shuttle.
shuttle-curse-system-failure = SYSTEM FAILURE
shuttle-curse-success-global = The shuttle will be delayed by {$time} minutes.

shuttle-curse-message-1 = A fuel technician just slit his own throat and begged for death.
shuttle-curse-message-2 = A scan of the shuttle's fuel tank has revealed tainting by a mixture of humanoid innards and teeth.
shuttle-curse-message-3 = A security incident involving a frenzied shuttle worker attacking coworkers with a laser cutter has just been reported as resolved by on-site security.
shuttle-curse-message-4 = A shuttle engineer began screaming 'DEATH IS NOT THE END' and ripped out wires until an arc flash seared off her flesh.
shuttle-curse-message-5 = A shuttle engineer was spotted inside the cockpit, feverishly arranging her innards on the floor in the shape of a rune before expiring.
shuttle-curse-message-6 = A shuttle inspector started laughing madly over the radio and then threw herself into an engine turbine.
shuttle-curse-message-7 = The corpse of an unidentified shuttle worker was found mutilated beyond recognition in the shuttle's main hold, with at least five unique sources of blood on the scene.
shuttle-curse-message-8 = The shuttle dispatcher was found dead with bloody symbols carved into their flesh.
shuttle-curse-message-9 = The shuttle's custodian was found washing the windows with their own blood.
shuttle-curse-message-10 = The shuttle's navigation programming was replaced by a file containing just two words: IT COMES.
shuttle-curse-message-11 = The shuttle's transponder is emitting the encoded message 'FEAR THE OLD BLOOD' in lieu of its assigned identification signal.

veil-shifter-description = It has {$charges} {$charges ->
    [one] charge
   *[other] charges
} left.
veil-shifter-cant-teleport = The veil won't part here. Try again!

whetstone-cant-sharpen = It can't get any sharper.
whetstone-sharpened = You sharpen {THE($weapon)}.

materials-runed-metal = runed metal
tiles-cult-floor = engraved floor

## Structures

timed-factory-cooldown = It is still recharging. Time left: {$cooldown}s
pylon-toggle-on = The pylon flares to life.
pylon-toggle-off = The pylon dims.
cult-pylon-burn-hand = The pylon sears your hand!
cult-barrier-destroyed = You dispel the barrier.

## Runes

name-selector-title = Name this rune
name-selector-accept-button = Accept

cult-rune-cant-draw = You can't draw a rune here!
cult-rune-cant-draw-rending = You have to be where the veil between our worlds is thinnest.
cult-rune-started-erasing = You start erasing the rune...
cult-rune-erased = The rune has been erased.
cult-rune-not-enough-cultists = Not enough cultists to perform the ritual! ({$current}/{$required})
cult-rune-no-targets = No targets found!

cult-offering-need-more-convert = Converting someone needs {$amount} cultists around the rune.
cult-offering-need-more-sacrifice = This one can't be converted. Sacrificing them alive needs {$amount} cultists around the rune.

cult-teleport-not-found = There are no other teleport runes.
cult-teleport-unnamed = Unnamed rune

cult-revive-rune-no-charges = Nar'Sie demands more offerings before she grants another life.
cult-buff-already-buffed = You are already empowered.
cult-blood-boil-rune-no-targets = There is no blood to boil in sight.

blood-cult-summon-being-pulled = They are being held and can't be summoned!
blood-cult-summon-cuffed = They are restrained and can't be summoned!

cult-rending-drawing-finished = The Geometer of Blood has finished drawing the rune of the end {$location}!
cult-rending-target-alive = The offering still lives! Nar'Sie can't be summoned yet.
cult-rending-already-summoning = The ritual is already under way.
cult-rending-started = The Geometer of Blood has begun the ritual of Dimensional Rending {$location}!
cult-rending-prevented = Someone has stopped the ritual.
