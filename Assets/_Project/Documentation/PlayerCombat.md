# Player combat

`Main.unity` now contains the playable cat and one `CombatTarget` using the bot prefab. The target is deliberately idle so player input, ranges, reactions and abilities can be tuned without combat AI interfering.

## Controls

- Left mouse: alternating procedural punch. While carrying a target, throws instead.
- Hold RMB: grab the closest target in the forward cone; release RMB to drop it. RMB continues to drive camera/facing while held.
- Left Shift: lift a held target. E is not bound to combat.
- Q: activate the selected ability. Invalid uses do not consume cooldown.
- Z / X: select the previous / next ability.
- RMB: unchanged — continues to rotate the camera and player heading.
- Space: unchanged — jump.
- Backspace: reset player combat, position, velocity and camera.

The HUD shows health, current combat phase, selected ability and cooldown.

## Rules

- Input has two presentation modes driven by `GameFlowState`: Lobby keeps RMB camera orbit; Party Match (`Playing`) uses continuous mouse-look, turns the player toward the camera heading, and keeps the camera rotating behind the player's actual facing. No held mouse button is required to face or choose attack direction. In the test scene, `L` advances the central flow through its legal transitions between Lobby and Playing.
- Left click still triggers Punch/Throw, RMB still holds Grab, Left Shift still requests Lift, and E remains unused.

- Punch uses a timed contact window, forward cone and per-action target de-duplication. Buffering near the end of a punch starts the next alternating punch without restarting the current hit window.
- Damage causes hit-stun and impulse. At zero health the cat is knocked out temporarily, then recovers at half health. Falling below the ring-out height respawns the same object at its registered spawn with full health.
- Grab links cannot form cycles. A held body is kinematic only while attached, ignores collision with its holder, and restores collision/gravity on drop, interruption or throw.
- Receiving a hit breaks both sides of a grab. Throw applies damage and a forward/upward impulse.
- Regular locomotion remains `Idle`, `Run`, `Jump_Air`. RMB grab uses only `Grab_Hold` while stationary and `Carry_Move` while moving, with loose procedural hand aiming; retained skill clips remain available as non-locomotion Animator states.

## Abilities

All eleven retained abilities are implemented with independent cooldowns:

- Dash: forward burst with a short contact cone.
- Nine Lives: usable only while knocked out; revives immediately.
- Hiss: usable only while held; breaks the grab and knocks the holder away.
- Pounce: requires a forward target and ground contact; launches toward it and hits on contact.
- Anchor: temporary knockback resistance and lower movement speed.
- Stomp: grounded radial damage and launch.
- Catnip: temporary movement-speed increase.
- Fluff Up: temporary damage multiplier.
- Hairball: pooled slippery ground zone.
- Shark Attack: pooled straight projectile.
- Wind Shurikens: two pooled three-projectile fans.

Projectiles and puddles use a prewarmed fixed pool; basic combat and per-frame status updates allocate no managed collections. Balance values are in `ScriptableObjects/Characters/CatCombat.asset`.

## Components

- `CatCombatant`: health, hit reaction, KO/recovery, status effects, grab links and respawn.
- `CatPlayerCombat`: input-independent player action state machine and ability validation.
- `CatCombatEffects`: projectile/hazard pool and collision resolution.
- `CatCombatPose`: procedural upper-body punch/grab presentation layered after the loose secondary motion.
- `CatCombatHud`: compact runtime debug HUD.

The bot prefab currently shares `CatCombatant` and reactions but has no attack AI. This keeps the requested scope on the player combat system.
