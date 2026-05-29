this is a game project called Skill Based Drone that is made using godot 4.6.1.stable.mono c#.

the code should be c# only, no gdscript

the architecture should be fairly code-first, except for scene node structure, so it's easy to tell what the structure looks like, and it's also easy to tell how the scene's behavior works just by looking at it's script.

`prefabs/` prefabs folder, should house prefabs
`scenes/` scenes folder, should house scenes/levels
`src/` c# source code
`src/scripts/` behavior scripts, usually for prefabs. example: `src/scripts/UnitScript.cs` will be for `prefabs/unit.tscn`

you can read the .tscn scene files to understand their structure

use 2 spaces for tab

avoid ifs without curly braces, and make sure to add the condition body in a new line

prefer using `var foo = bar;` instead of using the type in the declaration

avoid redundant private declarations

never use `[Export] string` fields for asset paths — that's the worst of both worlds. instead: use `GD.Load<PackedScene>("res://prefabs/foo.tscn")` at field init for things that don't need customization, and use `[Export] PackedScene` (or `[Export] AudioStream` , etc.) for things that vary per-instance and need to be set in the inspector

the drone should behave like an fpv drone with first person camera controls just like the battlefield helicopter control scheme, like this: W and S for throttle, A and D for yaw, mouse X for roll, mouse Y for pitch

when asked to enrich CLAUDE.md, enrich this file with stuff that you judge useful to know for future sessions, context that you are giving yourself that will help you solve problems and develop features. no need to write every single thing, just things that you think will help you. also, you can tweak other things on this file that you judge need or could be changed and improved

## Godot-specific gotchas

* never set `GlobalPosition` inside an object initializer or before `AddChild` — it requires the node to be in the scene tree. always call `AddChild` first, then set `GlobalPosition`
* `AudioStreamPlayer3D.GlobalPosition` has the same restriction — set it after `AddChild`
* `Node.Owner` is a reserved Godot property — don't name enums or members `Owner` inside Node subclasses, it causes CS0108. use a different name (e.g. `Faction`)
* `ParticleProcessMaterial.Damping` is a `Vector2` (min/max) in Godot 4 — use `DampingMin` and `DampingMax` instead
* `ParticleProcessMaterial.ColorRamp` expects a `GradientTexture1D`, not a raw `Gradient` — wrap it: `new GradientTexture1D { Gradient = gradient }`
* `Label3D` with `fixed_size = true` keeps consistent size across distances but NOT across FOV changes — for truly consistent screen-space size, use a 2D `Label` on a `CanvasLayer` and project world position each frame with `camera.UnprojectPosition()`
* SonarLint will warn about `res://` paths as "hardcoded URIs" — this is a false positive for Godot projects, ignore it
* SonarLint will warn about interface implementation on `Node` subclasses as redundant — also a false positive, ignore it

## Collision and physics

* `PhysicsRayQueryParameters3D` has separate `CollideWithAreas` and `CollideWithBodies` flags — if either is false the ray passes through those colliders silently. player bullets need both true to hit walls and enemies
* `IntersectShape` can be unreliable for splash damage across mixed collider types and layers — iterating `GetTree().GetNodesInGroup()` with a distance check is simpler and more predictable
* to use groups, call `AddToGroup("name")` in `_Ready()`. current groups: `"targets"` (DroneEnemyScript, TankScript), `"worm_segments"` (WormSegmentScript), `"missiles"` (MissileScript), `"flares"` (FlareScript)

## Architecture patterns used

* procedural mesh building in code (missile body, fins) using `CapsuleMesh`/`BoxMesh`/`SphereMesh` for geometry
* smoke trail: spawn `SmokeParticleScript` instances (SphereMesh) at fixed intervals (~60ms), each expands and fades in world space. smoke lifetime is short (1.2s) to keep transparent object count low
* damage numbers: `DamageNumberScript` extends `Node3D`, creates a 2D `Label` child on the player's `CanvasLayer`, projects world position each frame via `camera.UnprojectPosition()`
* explosion: `ExplosionScript` spawns layered `GpuParticles3D` (fireball, debris, sparks, smoke) + flash sphere + shockwave torus, all scaled by a `radius` param passed via `Init(float radius)` before `AddChild`. smoke particles are reparented to scene root before the explosion node frees so they outlive it
* long-lived particles that must outlive their parent: `RemoveChild(node)` → `GetTree().CurrentScene.AddChild(node)` → restore `GlobalPosition` → hook `node.Finished += node.QueueFree`

## Weapon system

* `WeaponScript` is a `Node3D` component placed as a child of `Mount` in the player scene. it owns: fire rate, projectile speed, damage, projectile prefab, shoot sound, and a `Barrel` (`Marker3D`) child as the spawn point
* `PlayerScript` discovers weapons at runtime via `GetNode<Node3D>("Mount").GetChildren().OfType<WeaponScript>()` — scene order is the contract: index 0 = primary (`fire`), index 1 = secondary (`fire_missile`)
* `ILaunchable` interface: `void Launch(Vector3 origin, Vector3 forward, float speed)` — implemented by both `BulletScript` and `MissileScript`. `WeaponScript` uses it to fire any projectile type uniformly. `BulletScript` gets special-cased first for the `damage` param, then falls through to `ILaunchable` for everything else
* `WeaponScript._timer` ticks in `_Process` every frame regardless of input — this ensures the first press always fires immediately. `TryFire()` takes no delta arg
* projectile orientation: `WeaponScript.TryFire` calls `projectile.LookAt(origin + forward, Vector3.Up)` after `AddChild` so the model faces the flight direction before `Launch` is called
* `BulletScript.Launch(origin, forward, owner, damage, speed)` — speed is now a param, not an export. default 900 m/s
* `MissileScript.Launch(origin, forward, forcedTarget, speed)` — speed sets the instance field used by homing physics too. default 100 m/s
* weapon prefabs: `prefabs/weapon_50cal.tscn`,  `prefabs/weapon_minigun.tscn`,  `prefabs/weapon_missile_launcher.tscn`
* to add a new gun: create a prefab with `WeaponScript` + `Barrel` marker, add it as a child of `Mount` in `player.tscn` at the desired position — `PlayerScript` picks it up automatically

## Missile system

* `MissileScript.Launch(origin, forward, forcedTarget, speed)` — pass a `forcedTarget` to lock the seeker onto a specific node (e.g. player) and skip `TryAcquireLock` entirely
* arming distance (default 4m) prevents self-detonation when launching at speed — collision detection is skipped until the missile has travelled that far
* `ApplySplash` checks the player node by path (`"Player"`) in addition to groups, so enemy missiles damage the player
* homing seeker searches `"targets"` group + `"worm_segments"` group — any `Node3D` in those groups is a valid lock
* flare seduction: `TrySeduceByFlare` runs each physics frame and overrides `_lockTarget` if a flare enters the seeker cone within `seekerRange`

## Flare system

* `FlareScript` adds itself to the `"flares"` group in `_Ready`. `MissileScript.TrySeduceByFlare` iterates this group and steals lock if a flare is in cone + range
* player fires flares with the `flare` input (X key), rate-limited by `flareRate` (default 0.4s) in `PlayerScript`
* flares are spawned at player `GlobalPosition` with `LinearVelocity` as initial velocity: `flare.Launch(GlobalPosition, LinearVelocity)`

## Performance gotchas

* every `new StandardMaterial3D` allocates a GPU descriptor — share static materials across instances for frequently-spawned objects (missiles, particles). D3D12 descriptor heap exhaustion shows as `uniform_set_create` errors
* `ImmediateMesh` rebuilt every frame per object is expensive in the transparent pass — avoid for frequently-spawned objects; use `GpuParticles3D` or pre-built meshes instead
* `SmokeParticleScript` shares a static `SphereMesh` but still creates one `StandardMaterial3D` per instance (needed for per-instance alpha fade). keep spawn rate and lifetime low to bound the count
* `Basis.Slerp` throws `InvalidOperationException` if the basis is not normalized — call `.Orthonormalized()` before `Slerp` on any basis that may have been affected by physics

## Health bar

* health bars use `prefabs/health_bar.tscn` (HealthBarScript) — a shared prefab instanced as a child named `HealthBar` in each enemy prefab
* `HealthBarScript` builds three `QuadMesh` children in code: dark background, amber ghost (damage indicator), green-to-red fill. all share the parent transform
* billboarding is done via `LookAt(_camera.GlobalPosition, Vector3.Up)` on the parent `Node3D` in `_Process` — do NOT put `BillboardMode` on individual materials, they will desync at oblique angles
* after `LookAt`, call `RotateObjectLocal(Vector3.Up, Mathf.Pi)` to flip the quads toward the camera (`QuadMesh` faces +Z, `LookAt` points -Z at target)
* guard `LookAt` with a distance check and a cross-product check to avoid degenerate up-vector cases
* `EnemyScript` calls `_healthBar?.Init(maxHealth)` after `OnReady()` and `_healthBar?.SetRatio(float)` in `TakeDamage`
* ghost effect: on damage, captures previous ratio as `_ghostRatio`, sets `_ghostAlpha = 1`, waits `GhostFadeDelay` then fades. ghost quad sits between fill's right edge and previous right edge
* fill quad X position: center-anchored, so left edge is at `-width/2`, right edge at `-width/2 + fillWidth`. ghost center: `fillRightEdge + ghostWidth/2`

## Enemy architecture

* `EnemyScript : Node3D` — base class for all enemies. owns: `maxHealth`, `explosionRadius`, `_health`, `_player`, `_camera`, `_canvasLayer`, `_healthBar`, `TakeDamage(int, Vector3)`, `SpawnExplosion`, `RandomizedFireTimer`
* subclasses override `OnReady()` (called before `_health = maxHealth`) to set `maxHealth`, `explosionRadius`, call `AddToGroup`, and do their own init
* `TankScript : EnemyScript` — ground enemy, fires missiles, moves toward player, snaps to ground
* `DroneEnemyScript : EnemyScript` — floating target, fires bullets, rotates to face player
* `WormSegmentScript : EnemyScript` — individual worm segment; overrides `TakeDamage` to notify `Worm.OnSegmentDied` before calling base when health hits 0
* `WormScript : Node3D` — NOT an EnemyScript subclass; it has no health itself, only segments do. manages segment chain movement and firing
* bullet/missile hit detection casts to `EnemyScript` directly — adding a new enemy subclass gets damage handling for free as long as it's in the right group
* staggered spawn: always init fire timers with `RandomizedFireTimer(fireRate)` (returns `GD.Randf() * fireRate`) to avoid all enemies firing simultaneously on first frame

## Ground enemies (TankScript)

* tanks spawn via `TargetSpawnerScript` using a downward raycast from a random XZ position to find terrain surface — `CollideWithAreas = false`,  `CollideWithBodies = true`
* tanks snap to ground each frame they move using the same raycast pattern
* `Basis.LookingAt` requires a non-zero, non-parallel direction — always compute `flatDir` from the XZ projection of `toPlayer` before calling it
* tank barrel node path: `Turret/Barrel` — cache it in `_Ready` to avoid `GetNode` exceptions mid-physics-process

## Sounds folder

audio files live in `sounds/` . notable files:
* `49483__lorenzosu__helicopterraw_30sec.wav` — drone blades loop
* `653191__sebastianvdb__close-gun-shots-trim.mp3` — gun shot
* `hitmarker.mp3` — hitmarker
* `268557__cydon__explosion_001.mp3` — explosion (5.6s, use `MaxDistance=2000`,  `UnitSize=100`)
* `521377__jarusca__rocket-launch.mp3` — missile launch
* `620077__ryankingart__metal-impact-trim.mp3` — player hull impact

## Scene structure (player)

player node path: `Player` (RigidBody3D, PlayerScript)
* `Mount` — camera + weapon mount, tilted slightly forward in local space
* `Mount/Camera3D` — main camera
* `Mount/Camera3D/CanvasLayer` — HUD canvas layer (used for damage numbers and HudScript)
* `Mount/Camera3D/CanvasLayer/Control/HealthLabel` — HP label
* `Mount/Weapon50Cal` — primary weapon (WeaponScript, index 0)
* `Mount/WeaponMissileLauncher` — secondary weapon (WeaponScript, index 1)
* `BladesAudio` — AudioStreamPlayer3D (drone rotor loop)
* `HitmarkerAudio` — AudioStreamPlayer
* `MetalImpactAudio` — AudioStreamPlayer (plays on collision and on TakeDamage)
* shoot audio is owned by each `WeaponScript` instance, not a top-level node

## Camera system

* single `Camera3D` under `Mount` — used for both FPV and TPV
* FPV: camera sits at its original local transform inside the tilted `Mount`
* TPV: `UpdateTpvMount()` runs each physics frame — resets `Mount` local basis to identity (removing the baked FPV tilt), places camera at `(0, 1.5, 4)` in local space (behind/above drone)
* toggle with V key; on switch back to FPV, restore both `_mount.Transform = _mountLocalTransform` and `_camera.Transform = _fpvLocalTransform`
* `Mount` is part of the drone body — never reparent or move it for camera purposes

## Input actions

defined in project input map:
* `throttle_up/down`, `yaw_up/down`, `roll_up/down`, `pitch_up/down` — flight axes
* `fire` — left mouse / primary fire
* `fire_missile` — right mouse / secondary fire
* `flare` — X key
* `switch_weapon` — Q key (cycles primary weapon index, not currently wired to secondary)
* `toggle_camera` — V key (defined in input map but handled via `key.Keycode == Key.V` in `_Input`)
