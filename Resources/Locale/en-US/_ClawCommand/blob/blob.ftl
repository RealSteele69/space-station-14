## Claw Command - blob antagonist

objective-issuer-blob = [color=#9a7a1f]Blob[/color]
roles-antag-blob-name = Blob
roles-antag-blob-objective = Grow until you reach critical mass.
role-subtype-blob = Blob
role-subtype-blob-zombie = Blob Zombie
guide-entry-blob = Blob
store-currency-display-blob-points = Blob points
chat-radio-blobmind = Blobmind
speak-vv-blob = Blob

blob-title = Blob
blob-description = A blob is loose on the station. Find it and burn it out before it reaches critical mass.

## Ghost roles

ghost-role-information-blob-name = Blob
ghost-role-information-blob-description = You are the blob infection. Grow and consume the station.
ghost-role-information-blob-rules = You are an antagonist. Grow, fight the crew and try to take over the station.
ghost-role-information-blobbernaut-name = Blobbernaut
ghost-role-information-blobbernaut-description = You are a blobbernaut. Defend the blob core. Talk to the blob with :l.
ghost-role-information-blobbernaut-rules = You are an antagonist on the blob's team. Defend your core and follow the blob.

blob-carrier-role-name = Blob carrier
blob-carrier-role-desc = A creature carrying the blob.
blob-carrier-role-rules = You are an antagonist. You have 10 minutes before you turn into a blob.
    Use that time to find a quiet spot on the station. You will be very weak right after you turn.
blob-carrier-role-greeting = You carry the blob. Find a quiet spot on the station and turn into the blob. Make the station yours and its crew your servants.
blob-carrier-transform-confirm = Press again to destroy your body and become the blob.

## Popups

blob-target-normal-blob-invalid = Wrong blob type, pick a normal blob.
blob-target-factory-blob-invalid = Wrong blob type, pick a factory blob.
blob-target-node-blob-invalid = Wrong blob type, pick a node blob.
blob-target-nearby-not-node = There is no node or core close enough.
blob-target-close-to-node = Too close to another node.
blob-target-already-produce-blobbernaut = This factory has already made a blobbernaut.
blob-target-already-connected = That node already has one of those.
blob-cant-split = You can't split your core again.
blob-not-enough-resources = You need { $point } more points.
blob-mob-attack-blob = You can't attack the blob.
blob-get-resource = +{ $point }
blob-spent-resource = -{ $point }
blobberaut-not-on-blob-tile = You are wasting away off the blob!
blobberaut-factory-destroy = Your factory is gone, you are wasting away!
carrier-blob-alert = You have { $second } seconds left before you turn into the blob.
blob-no-using-guns-popup = Your fingers can't work the trigger.

blob-mob-zombify-second-start = { CAPITALIZE(THE($pod)) } starts climbing onto your head!
blob-mob-zombify-third-start = { CAPITALIZE(THE($pod)) } starts climbing onto { THE($target) }'s head!
blob-mob-zombify-second-end = { CAPITALIZE(THE($pod)) } takes over your body!
blob-mob-zombify-third-end = { CAPITALIZE(THE($pod)) } takes over { THE($target) }!

## Chemical swap window

blob-chem-swap-ui-window-name = Swap chemicals
blob-chem-reactivespines-info = Reactive Spines
    Deals 5 blunt, 5 slash, 5 piercing and 100 structural damage.
blob-chem-blazingoil-info = Blazing Oil
    Deals 10 burn and 100 structural damage and sets the target on fire.
blob-chem-regenerativemateria-info = Regenerative Materia
    Deals 10 toxin and 80 structural damage. Resource blobs make 1 extra point.
blob-chem-explosivelattice-info = Explosive Lattice
    Deals 4 burn and 100 structural damage and sets off a small blast on the target. Spores explode when they die.
    The blob is immune to explosions, but takes more burn and shock damage.
blob-chem-electromagneticweb-info = Electromagnetic Web
    Deals 12 shock and 100 structural damage, with a 15% chance of an EMP when attacking.
    Dying blob tiles sometimes let out an EMP. The blob takes a little more brute and burn damage.
blob-chem-comatosefiber-info = Comatose Fiber
    Deals 14 asphyxiation and 100 structural damage. Growing over open space costs half as much.
blob-chem-chaincoating-info = Chain Coating
    Deals 8 blunt, 8 slash and 100 structural damage.
    The blob is much tougher against blows, but grows and pulses slower and makes fewer points.
blob-chem-sinewytendons-info = Sinewy Tendons
    Mends 5 of most damage types on whatever it hits.
    Pulses faster, factories hold 3 spores and factories and resource blobs are cheaper.
blob-chem-corrosiveslime-info = Corrosive Slime
    Deals 200 structural, 9 acid and 1 cellular damage. Nodes cost 120 points.
blob-chem-cryogenicpoison-info = Cryogenic Poison
    Deals 11 cold, 3 slash and 70 structural damage.

## Announcements

blob-announcement-sender = Central Command
alert-level-delta-blob-announcement = A level 5 biohazard has reached critical mass and the station is under threat of imminent destruction. Crewmembers are advised to listen to heads of staff for more information.
blob-warp-core = blob core
blob-alert-out-off-station = A blob core was removed because it was outside the station.
blob-alert-recall-shuttle = The emergency shuttle can't be sent while a level 5 biohazard is spreading on the station.
blob-alert-detect = Confirmed outbreak of a level 5 biohazard aboard the station. All personnel must contain the outbreak.
blob-alert-critical = Biohazard level critical. Nuclear authentication codes have been sent to the station. Central Command orders any remaining personnel to activate the self-destruct.
blob-alert-critical-NoNukeCode = Biohazard level critical. Central Command orders any remaining personnel to seek shelter and wait for rescue.
blob-alert-shuttle-arrived = Biohazard detected on board. All personnel must evacuate immediately.

## Verbs

blob-pod-verb-zombify = Raise
blob-verb-upgrade-to-strong = Upgrade to strong blob
blob-verb-upgrade-to-reflective = Upgrade to reflective blob
blob-verb-upgrade = Upgrade

## Alerts

blob-resource-alert-name = Blob points
blob-resource-alert-desc = Points made by your core and resource blobs. Spend them to grow and build special blobs.
blob-health-alert-name = Core health
blob-health-alert-desc = Your core's health. If it reaches zero, the blob dies.

## Greetings

blob-role-greeting =
    You are the blob, a space parasite that can swallow whole stations.
        Your goal is to survive and grow as big as you can.
        Blows only hurt you a little, but fire and lasers hurt a lot.
        Click next to your blob to grow, click on things to attack them. Alt-click a normal blob to make it strong, and a strong blob to make it reflective.
        Place resource blobs next to your core or a node to make points, and nodes to grow further out.
        Talk to your minions with :l.
blob-zombie-greeting = A blob spore took over your body. Help the blob take the station. Talk to the blob with :l.

## Round end

blob-round-end-result =
    { $blobCount ->
        [one] There was one blob infection.
        *[other] There were {$blobCount} blob infections.
    }
blob-user-was-a-blob = [color=gray]{$user}[/color] was a blob.
blob-user-was-a-blob-named = [color=White]{$name}[/color] ([color=gray]{$user}[/color]) was a blob.
blob-was-a-blob-named = [color=White]{$name}[/color] was a blob.

## Objective

objective-condition-blob-capture-title = Take over the station
objective-condition-blob-capture-description = Take over the whole station. You need at least {$count} blob tiles.

## Admin

admin-verb-make-blob = Make the target a blob carrier.
admin-verb-text-make-blob = Make Blob Carrier

## Speech heard by the crew

accent-words-blob-1 = Blob.
accent-words-blob-2 = Glub glub...
accent-words-blob-3 = Bleb bob blub!
accent-words-blob-4 = Gloooob.
accent-words-blob-5 = Blub?
