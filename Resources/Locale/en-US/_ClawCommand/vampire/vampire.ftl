# --- vampire
admin-verb-make-vampire = Make the target into a vampire.
admin-verb-text-make-vampire = Make Vampire

vampire-roundend-name = Vampire
roles-antag-vampire-name = Vampire
roles-antag-vampire-description = Feed on the crew to grow in power, and survive the night. Fear the sun.
role-subtype-vampire = Vampire
guide-entry-vampire = Vampire

vampire-fullname = {$title} {$name}

vampire-role-greeting = You are {$fullName}, a Vampire - a bloodsucker who has clawed their way back from the grave.
  You regenerate wounds slowly on borrowed blood, you dread direct sunlight, and you will wither to nothing if your stolen blood runs dry.
  Other Vampires are not necessarily your friends, but your survival may depend on their cooperation.
  Rest in a coffin to claim it - and the room around it - as your lair. Only your coffin can protect you when the sun rises.

alert-vampire-blood-name = Blood
alert-vampire-blood-desc = How much stolen blood is sustaining you. Run dry, and you will starve.

vampire-feed-fail-no-grab = You need at least a firm grip on them to feed.
vampire-feed-fail-no-blood = There is nothing left in them to drink.
vampire-feed-start = You sink your fangs into {$target}.
vampire-feed-stop = You release your hold.

vampire-masquerade-on = Your skin flushes with false warmth and your heart begins a hollow, borrowed rhythm.
vampire-masquerade-off = You let the act drop. The warmth fades, and your stillness returns.
vampire-masquerade-fail-no-blood = You don't have the blood to spare to keep up appearances.

vampire-fortitude-on = Your flesh hardens, cold and unyielding as grave-stone.
vampire-fortitude-off = Your flesh loosens back to its usual give.
vampire-fortitude-fail-no-blood = You don't have the blood to spare to toughen yourself.

vampire-cloak-on = You fade into the shadows.
vampire-cloak-off = The shadows release you.
vampire-cloak-fail-no-blood = You don't have the blood to spare to cloak yourself.

vampire-coffin-claimed = You claim this coffin as your lair. Rest here, lid shut, to heal quickly without spending a drop of blood.

vampire-rankup-verb-stats = Grow in power
vampire-rankup-verb-power = Grow in power: learn {$power}
vampire-rankup-success = Your blood thickens. You are now a rank {$level} vampire.


vampire-mesmerize-fail-target = That won't work on them.
vampire-mesmerize-fail-no-blood = You don't have the blood to spare to mesmerize anyone.
vampire-mesmerize-start = You lock eyes with {$target} and hold their stare...
vampire-mesmerize-success = Your mind goes blank, and your legs give out beneath you.

roles-antag-vassal-name = Vassal
roles-antag-vassal-objective = Serve and protect the Vampire who embraced you.
role-subtype-vassal = Vassal
objective-issuer-vampire = Vampire
vassal-objective-keep-master-alive-title = Keep {$name} from meeting their final death.

vampire-embrace-fail-no-capacity = You cannot command any more thralls at your current rank.
vampire-embrace-fail-target = They cannot be embraced.
vampire-embrace-start = You lean in close over {$target}, baring your fangs...
vampire-embrace-success-master = {$target} rises as your Vassal, bound to your fate.
vampire-embrace-success-vassal = Your will is no longer entirely your own. You have been embraced as {$master}'s Vassal - while they live, so does your purpose.
vampire-embrace-freed = The pull on your will vanishes. You are free.

vassal-recuperate-fail-no-blood = You're too drained to spare any more blood recuperating.
vassal-sense-master-gone = You feel nothing - your master is gone.
vassal-sense-master-unknown = You cannot sense them from here.
vassal-sense-master-distance = You feel the pull of your master's blood - roughly {$distance} meters away.

vampire-power-fail-staked = Not with a stake through your heart.
vampire-stake-start = You drive the stake toward {$target}'s heart...
vampire-stake-final-death = The stake finds its mark. {$target} goes still - this time, for good.
vampire-stake-disabled = The stake sinks into your chest. Your power drains away as long as it stays there.

## Sol - the solar flare cycle
vampire-sol-warn-early = Solar flares will bombard the station with dangerous UV in about a minute and a half. Prepare to seek cover in a coffin or closet.
vampire-sol-warn-final = Solar flares are about to bombard the station! You have 25 seconds to find cover!
vassal-sol-warn-final = In 25 seconds your master will be at risk of a solar flare. Make sure they find cover!
vampire-sol-warn-imminent = Seek cover, for Sol rises!
vampire-sol-start = Solar flares bombard the station with deadly UV light! Stay in cover for the next minute or risk final death!
vassal-sol-start = Solar flares bombard the station with UV light!
vampire-sol-end = The solar flare has ended, and the daylight danger has passed... for now.
vampire-sol-locker = Your skin sizzles. This doesn't protect well against the UV bombardment.
vampire-sol-burn = The solar flare sets your skin ablaze!
vampire-sol-burn-fledgling = The solar flare scalds your neophyte skin!

## Ranks
vampire-rankup-gained = You have grown more ancient! Sleep in a coffin you have claimed to thicken your blood and become more powerful.
vampire-rankup-gained-coffin = You have grown more ancient! Right-click yourself while resting here to grow in power.
vampire-rankup-gained-no-coffin = You have grown more ancient, but you have no coffin to rest in. Close yourself inside any closet or crate to claim it - you can always build one from wooden planks.

## Torpor and final death
vampire-torpor-begin = Your immortal body will not yet relinquish your soul to the abyss. You enter torpor, and your wounds slowly close.
vampire-torpor-end = You have recovered from torpor.
vampire-final-death-fire = {CAPITALIZE(THE($target))} burns past the point of any return.
vampire-final-death-organs = {CAPITALIZE(THE($target))} goes still. Without a heart and a mind, there is nothing left to return.

## Garlic
vampire-garlic-ingested = Garlic! It churns in your gut and smothers your power.
vampire-power-fail-garlic = Garlic in your blood is interfering with your powers!
vampire-power-fail-garlic-necklace = The necklace on your neck is interfering with your powers!

## Stakes
vampire-stake-fail-standing = You can't stake {THE($target)} while they're on their feet! They have to be lying down.
vampire-stake-missed-heart = You missed {THE($target)}'s heart! It would be easier if they weren't struggling so much.

construction-graph-tag-garlic = garlic
construction-graph-tag-candle = candle
