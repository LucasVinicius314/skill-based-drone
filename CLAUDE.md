this is a game project called Skill Based Drone that is made using godot 4.6.1.stable.mono c#.

the code should be c# only, no gdscript

the architecture should be fairly code-first, except for scene node structure, so it's easy to tell what the structure looks like, and it's also easy to tell how the scene's behavior works just by looking at it's script.

`prefabs/` prefabs folder, should house prefabs
`scenes/` scenes folder, shold house scenes/levels
`src/` c# source code
`src/scripts/` behavior scripts, usually for prefabs. example: `src/scripts/UnitScript.cs` will be for `prefabs/unit.tscn`

you can read the .tscn scene files to understand their structure

use 2 spaces for tab

avoid ifs without curly braces, and make sure to add the condition body in a new line

prefer using `var foo = bar;` instead of unsing the type in the declaration

avoid redundant private declarations

when referencing a known prefab for instantiation, use attributes like this `PackedScene orePrefab = GD.Load<PackedScene>("prefabs/ore.tscn");`

the drone should behave like an fpv drone with first person camera controls just like the battlefield helicopter control scheme, like this: W and S for throttle, A and D for yaw, mouse X for roll, mouse Y for pitch

## Godot-specific gotchas

* never set `GlobalPosition` inside an object initializer or before `AddChild` — it requires the node to be in the scene tree. always call `AddChild` first, then set `GlobalPosition`
* `AudioStreamPlayer3D.GlobalPosition` has the same restriction — set it after `AddChild`
* `Node.Owner` is a reserved Godot property — don't name enums or members `Owner` inside Node subclasses, it causes CS0108. use a different name (e.g. `Faction`)
* `ParticleProcessMaterial.Damping` is a `Vector2` (min/max) in Godot 4 — use `DampingMin` and `DampingMax` instead
* `ParticleProcessMaterial.ColorRamp` expects a `GradientTexture1D`, not a raw `Gradient` — wrap it: `new GradientTexture1D { Gradient = gradient }`
* `Label3D` with `fixed_size = true` keeps consistent size across distances but NOT across FOV changes — for truly consistent screen-space size, use a 2D `Label` on a `CanvasLayer` and project world position each frame with `camera.UnprojectPosition()`
* SonarLint will warn about `res://` paths as "hardcoded URIs" — this is a false positive for Godot projects, ignore it

## Collision and physics

* `PhysicsRayQueryParameters3D` has separate `CollideWithAreas` and `CollideWithBodies` flags — if either is false the ray passes through those colliders silently. player bullets need both true to hit walls and enemies
* `IntersectShape` can be unreliable for splash damage across mixed collider types and layers — iterating `GetTree().GetNodesInGroup()` with a distance check is simpler and more predictable
* to use groups, call `AddToGroup("name")` in `_Ready()`. current groups: `"targets"` (TargetScript),  `"worm_segments"` (WormSegmentScript)

## Architecture patterns used

* procedural mesh building in code (missile body, fins, trails) using `MeshInstance3D` + `ImmediateMesh` for trails,  `CapsuleMesh`/`BoxMesh`/`SphereMesh` for geometry
* smoke trail: spawn `SmokeParticleScript` instances (billboard `QuadMesh`) at fixed intervals, each expands and fades in world space
* damage numbers: `DamageNumberScript` extends `Node3D`, creates a 2D `Label` child on the player's `CanvasLayer`, projects world position each frame via `camera.UnprojectPosition()`
* explosion: `ExplosionScript` spawns layered `GpuParticles3D` (fireball, debris, sparks, smoke) + flash sphere + shockwave torus, all scaled by a `radius` param passed via `Init(float radius)` before `AddChild`. smoke particles are reparented to scene root before the explosion node frees so they outlive it
* long-lived particles that must outlive their parent: `RemoveChild(node)` → `GetTree().CurrentScene.AddChild(node)` → restore `GlobalPosition` → hook `node.Finished += node.QueueFree`

## Sounds folder

audio files live in `sounds/` . notable files:
* `49483__lorenzosu__helicopterraw_30sec.wav` — drone blades loop
* `653191__sebastianvdb__close-gun-shots-trim.mp3` — gun shot
* `hitmarker.mp3` — hitmarker
* `268557__cydon__explosion_001.mp3` — explosion (5.6s, use `MaxDistance=2000`,  `UnitSize=100`)
* `521377__jarusca__rocket-launch.mp3` — missile launch

## Scene structure (player)

player node path: `Player` (RigidBody3D, PlayerScript)
* `Mount/Barrel` — gun barrel, used as bullet spawn point
* `Mount/MissileLauncher/Barrel` — missile barrel
* `Mount/Camera3D` — main camera
* `Mount/Camera3D/CanvasLayer` — HUD canvas layer (used for damage numbers)
* `Mount/Camera3D/CanvasLayer/Control/HealthLabel` — HP label
* `BladesAudio`,  `ShootAudio` — AudioStreamPlayer3D
* `HitmarkerAudio` — AudioStreamPlayer

## Input actions

defined in project input map:
* `throttle_up/down`,  `yaw_up/down`,  `roll_up/down`,  `pitch_up/down` — flight axes
* `fire` — left mouse / primary fire
* `fire_missile` — right mouse / missile
