# nighttrain

Night Train - Rail Shooter Mod for GTA V

## rough idea/plan

The player spawns driving the train, they need to survive a lap

Player is quite vulnerable when train speed is 5-10, and that's also a good
cruising speed to be able to actually interact with surroundings - the train
looks really cool going at 50, 100 or even 200, but it's not really possible
for them to hit targets off to the side or even for most enemies to hit them.

Despite that, it *does* look very cool when the train is going fast - we might 
have a few scenarios that can spawn that speed the train up massively to eg
splatter enemy groups on the tracks, or even attach a ramp to the front to send
things flying everywhere - use it sparingly, because normal gameplay loop is 
broken when fast

We need a way to enable the player to aim and shoot - trains prohibit this

Some ideas, from easy to hard:

1. Glue player to top of train, they can turn, aim, shoot etc as usual but 
   can't move, or be knocked off, or ragdoll etc - we may also need to see if we
   can disable collisions so that they don't get knocked off by eg low 
   overpasses etc - because they're "on foot" they can use any of their weapons

2. Spawn a vehicle "inside" the train engine, harden it, attach it to train so
   that it rotates with it and etc, make it invisible and place the player in 
   it, potentially making the player invisible and placing a clone in the
   train engine seat for visual purposes - player can shoot from vehicle with
   usual limitation, eg limited set of drive-by compatible weapons

3. As above but use a weaponised vehicle and place player in turret seat

4. Think of other ways here

We then spawn along the track various types of scenarios:

### ambushes

Testing with just a single ped on the track at the moment, but we will instead 
set up various ambushes - group of peds around the tracks, weaponised vehicles 
etc

### blockades

Things on the track that will harm the player if not dealt with (by shooting 
them?)

Might be eg wall of fire, concrete barrier, petrol tanker etc

### pickups

Things on the track that will give the player good or bad effects when collided
with - mostly good - perhaps can shoot bad pickups, so they're sort of a 
blockade?

Could be eg temporary speedup, permanent speedup (but small, see note above
about speed), spawn an ally on train and glue them in place, temporary 
forcefield etc - lots of cool things we could do with pickups

### targets

Like pickups, spawn around/above track in groups, can be shot to get the effect

Can also have things like:

Choice of target - three targets with different symbols appear, player gets the
first one that they shoot, despawning the others

Opportunity for multiple - spawn in a group, each effect claimed has 50% chance
of despawning rest of group - so depending on rng you might only get one or 
might be lucky and get them all

"Loot box" - don't know what it is til you hit it, small chance of being bad

## other ideas

Some other ideas include utilizing the 5 stations around the map for 
something - maybe a cinematic with scripted camera where the train slows down 
and stops, and some kind of advantage is applied - an even more ambitious 
version of this could have the player awarded points and be able to spend them
in a station menu for various advantages/effects when stopped - I was also
considering having checkpoints that the player can resume from, though I'm 
leaning away from this idea in favour of a more permadeath playstyle, if I did
decide to do it, maybe stations could be checkpoints? Though they're not 
distributed evenly around the loop, so maybe not!
