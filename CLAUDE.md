this is a game project called Skill Based Drone that is made using godot 4.7.2.stable.mono c#, jolt physics, forward+, d3d12.

the code should be c# only, no gdscript

the architecture should be fairly code-first, except for scene node structure, so it's easy to tell what the structure looks like, and it's also easy to tell how the scene's behavior works just by looking at it's script.

## layout

* `prefabs/` reusable scenes (node structure only), `scenes/` menu and world, `sounds/`, `models/`, `shaders/`, `skyboxes/`, `scripts/` (build/setup scripts, not game code), `libs/p2p-signaling-server` (git submodule, **never edit**)
* `src/` is organised by feature, not by file kind. each feature only uses another feature's public api:
  * `Core` — depends on nothing (`ILaunchable`, `DevArgs`)
  * `Player` — `PlayerScript` (the drone)
  * `Weapons` — `WeaponScript`, `BulletScript`, `MissileScript`, `FlareScript`, `Projectiles` (spawns the shots every peer replays)
  * `Enemies` — `EnemyScript` base, `DroneEnemyScript`, `TankScript`, `WormScript`, `WormSegmentScript`, `HealthBarScript`, `EnemySpawner`
  * `World` — `Terrain` (procedural ground, see below)
  * `Effects` — explosions, smoke, damage numbers, bullet impacts
  * `UI` — `HudScript`, `PauseMenuScript`, `MainMenu` (HUD and menus are built in code)
  * `Net` — `NetSession` (autoload), `ServerSettings`, `PlayerProfile`, `FriendsFile`, `NetDevHooks`
  * `Main.cs` — the script of `scenes/main_scene.tscn`: starts the session the menu asked for and spawns the drones
* scripts keep the project's existing names (`XScript` for older ones, plain names for newer ones) and live in the global namespace. a script's folder is its feature; prefab `foo.tscn` is driven by the script it names
* you can read the .tscn scene files to understand their structure. the editor re-saves them (adds uids, drops defaults): re-read before editing one by hand

## code style

use 2 spaces for tab

avoid ifs without curly braces, and make sure to add the condition body in a new line

prefer using `var foo = bar;` instead of using the type in the declaration

avoid redundant private declarations

never use `[Export] string` fields for asset paths — that's the worst of both worlds. instead: use `GD.Load<PackedScene>("res://prefabs/foo.tscn")` at field init for things that don't need customization, and use `[Export] PackedScene` (or `[Export] AudioStream` , etc.) for things that vary per-instance and need to be set in the inspector

the drone should behave like an fpv drone with first person camera controls just like the battlefield helicopter control scheme, like this: W and S for throttle, A and D for yaw, mouse X for roll, mouse Y for pitch

when asked to enrich CLAUDE.md, enrich this file with stuff that you judge useful to know for future sessions, context that you are giving yourself that will help you solve problems and develop features. no need to write every single thing, just things that you think will help you. also, you can tweak other things on this file that you judge need or could be changed and improved

## commands

* first time on a machine: `git submodule update --init`, `scripts/fetch-webrtc.ps1` (or `.sh`; downloads `addons/webrtc_native`, about 90 MB, pinned and checksummed, git-ignored), then `dotnet build SkillBasedDrone.csproj`
* clone with `--recurse-submodules`; CI must check out submodules and run the fetch script before exporting. the exported build needs the webrtc native library next to the executable or multiplayer fails with `webrtc_unavailable`
* build and release: `build.ps1` / `build.sh` (`-Target windows|linux`, `-Package`, `-InstallTemplates`; see BUILDING.md; needs `GODOT` env var or a git-ignored `.godot-path` file pointing at the console exe). pushing a tag `vX.Y.Z` on main runs `.github/workflows/release.yml`, which publishes `SkillBasedDrone-windows.zip` and `SkillBasedDrone-linux.tar.gz` (the names `README.md` and `scripts/update/` depend on; the updater assumes the repo is `LucasVinicius314/skill-based-drone`). each build folder gets `version.txt` and the updater
* import / smoke test: `Godot_..._console.exe --headless --path . --import`
* the game runs from `scenes/menu.tscn`. `scenes/main_scene.tscn` is the world (do not set it as the main scene: it needs the session the menu prepares, though `--solo` skips the menu)

## multiplayer

the p2p wiring rules (library compiled into this assembly, assign the peer in `PeerCreated`, RPCs only after `ConnectedToServer`/`PeerConnected`, `addons/webrtc_native` present) are in `libs/p2p-signaling-server/docs/integration-guide.md`: read it before touching networking. bucket: `skill-based-drone` (must exist on the server; create it with the server's `create_bucket` MCP tool or admin REST api, 8 players, star topology). **the game contains no server address**: the library reads `P2P_SIGNALING_URL` or the shared `~/.p2p-signaling-server/server` file, which the menu's Settings page writes. never set `P2PLobby.Url`. the player name is shared the same way (`~/.p2p-signaling-server/name`)

design: **every peer flies and simulates its own drone, the host owns the enemies.**

* `NetSession` is the autoload at `/root/NetSession`. it owns the lobby, the handshake (`RpcHello` -> host adds the peer to the roster and raises `PeerReady`), roster, ping, and the one-off effects every peer replays (`AnnounceShot`, `AnnounceFlare`, `EnemyBullet`, `EnemyMissile`). clients can only talk to the host (star), so client shots go `RpcShotRequest` -> host replays and relays to the others
* `Main` builds drones with a `MultiplayerSpawner` + custom spawn function (data = peer id and position). **the node name is the owning peer id** and `PlayerScript._EnterTree` derives its authority from it (setting authority inside the spawn function instead made the spawn never reach that peer). `PlayerScript.IsLocal` = this peer owns it. a non-local drone is a frozen kinematic puppet: no camera, HUD or input, it eases toward `SyncPosition/SyncRotation/SyncVelocity` (replicated by the `Synchronizer` in `player.tscn`) and shows a blue name tag with the distance to it and a fixed-size blue halo. statics: `PlayerScript.Local`, `ByPeer(id)`, `Nearest(pos)`
* in the hosting world, enemies and the host's drone are spawned in `OnRoomOpen` (after `CodeReady`), **not at `_Ready`**: assigning the lobby's multiplayer peer resets the spawners, so anything spawned earlier never reaches clients
* enemies: `EnemySpawner` (a `MultiplayerSpawner`, host only, spawn data = kind, position, id; node name `Enemy<id>`). only the host runs the AI (`EnemyScript.Simulate(delta)` at the top of every `_PhysicsProcess`, which also picks the nearest drone as `_player`); clients follow `SyncPosition/SyncRotation/SyncHealth` (smoothed in `_Process`). a hit on a client sends `RpcRequestDamage` to the host (validated: positive, at most 10). on death the host sets health 0, clients see it, explode and vanish; the host frees the node 0.5 s later (a spawner then removes it everywhere). late joiners see already-dead enemies vanish silently (age under 1 s)
* the worm is one spawned node whose segments every peer builds in `_Ready` with fixed names (`Segment0..`); the worm's own synchronizer carries all positions and healths (`SyncPositions`, `SyncHealths`, assigned whole so they replicate). **do not put synchronizers on script-created grandchildren**: the host starts sending before the client has built them and floods the log with `Node not found`
* shots: the local player's shot is real; other peers replay it from the puppet's weapon (`ReplayShot` -> `WeaponScript.SpawnProjectile(origin, forward, mine: false)`), where it only looks and sounds right (`Authoritative = false`: no enemy damage). enemy shots are spawned on every peer by `Projectiles` and only the peer whose drone is hit takes the damage (`PlayerScript.IsLocal` check in `BulletScript`; `MissileScript` splash on `PlayerScript.Local`). enemy missiles home on `PlayerScript.ByPeer(targetPeer)`
* pausing: online the world keeps running (`PauseMenuScript` only dims and frees the mouse; `PlayerScript` ignores controls while it is open); solo really pauses. `R` and RESTART are solo only; MAIN MENU leaves the session
* new network messages go in `NetSession` (host-to-client RPCs use the default authority mode, client-to-host `AnyPeer` and must validate the sender, `GetRemoteSenderId`, and bounds). bump `NetSession.Protocol` when the handshake or an RPC contract changes incompatibly
* not done: players cannot damage each other, no respawn after death (the drone just takes damage), player health is not shown to others, no host migration, no late-join catch-up of flares or missiles in flight

dev flags (after `--`): `--solo`, `--net-host`, `--net-join=CODE`, `--net-name=`, `--net-url=http://127.0.0.1:8099`, `--net-no-friends` start without the menu; `--net-shoot-at=SEC`, `--net-tpv`, `--net-report-at=SEC`, `--screenshot=FILE --screenshot-at=SEC`, `--net-quit-at=SEC`, `--menu-screenshot=FILE --menu-page=host|join|settings` (see `NetDevHooks`). two-process check: local server `go run ./cmd/devserver -bucket skill-based-drone` inside the submodule, host prints `CODE xxxx`, start the client with that code and **a different `P2P_SIGNALING_HOME`**, compare the `NET REPORT` lines of both. a compile proves nothing for networking

## Godot-specific gotchas

* never set `GlobalPosition` inside an object initializer or before `AddChild` — it requires the node to be in the scene tree. always call `AddChild` first, then set `GlobalPosition` (a plain `Position` before `AddChild` is fine, the spawners do it)
* `AudioStreamPlayer3D.GlobalPosition` has the same restriction — set it after `AddChild`
* `Node.Owner` is a reserved Godot property — don't name enums or members `Owner` inside Node subclasses, it causes CS0108. use a different name (e.g. `Faction`)
* `ParticleProcessMaterial.Damping` is a `Vector2` (min/max) in Godot 4 — use `DampingMin` and `DampingMax` instead
* `ParticleProcessMaterial.ColorRamp` expects a `GradientTexture1D`, not a raw `Gradient` — wrap it: `new GradientTexture1D { Gradient = gradient }`
* `Label3D` with `fixed_size = true` keeps consistent size across distances but NOT across FOV changes — for truly consistent screen-space size, use a 2D `Label` on a `CanvasLayer` and project world position each frame with `camera.UnprojectPosition()`
* SonarLint will warn about `res://` paths as "hardcoded URIs" — this is a false positive for Godot projects, ignore it
* SonarLint will warn about interface implementation on `Node` subclasses as redundant — also a false positive, ignore it
* properties replicated by a `MultiplayerSynchronizer` must be `[Export]`. an array or other reference property only replicates when it is assigned, not when an element changes
* `MultiplayerSynchronizer` replication config in a `.tscn`: `properties/N/path = NodePath(".:Prop")`, `spawn = true`, `replication_mode` 1 = always (unreliable), 2 = on change
* a multiplayer node needs the same path on every peer: name spawned nodes deterministically
* the bash tool chokes on apostrophes inside heredocs: write scripts and files with the Write tool

## Terrain

* `Terrain` (node `Terrain` in `main_scene.tscn`) builds the ground in code at startup: a 2048 m square heightfield (16x16 chunks), 2 m cells, 8x8 chunks, each a `MeshInstance3D` with vertex colors (grass, dirt on the flat pad, rock on slopes, snow high up) and a trimesh `StaticBody3D`. no textures, no addon. the look comes from `shaders/terrain.gdshader` (vertex color is the base; the shader adds procedural patches, grain, dry earth, rock strata and a bumpy normal in world space, fading with distance) plus the fog and filmic tonemap in the player camera's `Environment`
* the height is a pure C# function of (x, z) with fixed constants and its own gradient noise (`Terrain.HeightAt`), so every peer builds the identical ground and nothing is sent over the network. it is usable before the node exists: `EnemySpawner` places tanks and keeps flying enemies above the ground with it. change the shape by editing the constants at the top of `Terrain.cs` (flat pad radius, hill/ridge/rim heights)
* a flat start pad (about 45 m radius) at `Terrain.PadCenter` (against the +Z border, y = -1) is the fixed drone spawn (`Main.SpawnOrigin`, drones face -Z); hills grow outward and a mountain rim closes the map. startup cost is about two seconds (rows sampled in parallel)
* `Structures` (node in `main_scene.tscn`) builds concrete compounds (tower, bunkers, walls) at fixed deterministic sites on flat ground; `Structures.Blocked(x, z, margin)` keeps spawners out of them. `EnemySpawner` picks spots inside the rim, away from the pad and structures, ground units on gentle low ground
* dev flag `--dev-pos=x,y,z,yawDeg` puts the drone somewhere for a screenshot

## Collision and physics

* `PhysicsRayQueryParameters3D` has separate `CollideWithAreas` and `CollideWithBodies` flags — if either is false the ray passes through those colliders silently. player bullets need both true to hit walls and enemies
* never `new PhysicsRayQueryParameters3D` every frame: it piles up native objects that only a GC frees (log spam `Leaked unsafe reference` and a crash at quit). keep one per object and set `From`/`To`
* `IntersectShape` can be unreliable for splash damage across mixed collider types and layers — iterating `GetTree().GetNodesInGroup()` with a distance check is simpler and more predictable
* to use groups, call `AddToGroup("name")` in `_Ready()`. current groups: `"targets"` (DroneEnemyScript, TankScript), `"worm_segments"` (WormSegmentScript), `"missiles"` (MissileScript), `"flares"` (FlareScript). a dead enemy leaves its groups and zeroes its `Area3D` collision layers so nothing targets or hits it

## Architecture patterns used

* procedural mesh building in code (missile body, fins) using `CapsuleMesh`/`BoxMesh`/`SphereMesh` for geometry
* smoke trail: spawn `SmokeParticleScript` instances (SphereMesh) at fixed intervals (~60ms), each expands and fades in world space. smoke lifetime is short (1.2s) to keep transparent object count low
* damage numbers: `DamageNumberScript` extends `Node3D`, creates a 2D `Label` child on the local player's `CanvasLayer` (`PlayerScript.HudLayer`), projects world position each frame via `camera.UnprojectPosition()`
* explosion: `ExplosionScript` spawns layered `GpuParticles3D` (fireball, debris, sparks, smoke) + flash sphere + shockwave torus, all scaled by a `radius` param passed via `Init(float radius)` before `AddChild`. smoke particles are reparented to scene root before the explosion node frees so they outlive it
* long-lived particles that must outlive their parent: `RemoveChild(node)` → `GetTree().CurrentScene.AddChild(node)` → restore `GlobalPosition` → hook `node.Finished += node.QueueFree`

## Damage scale

health and damage are whole numbers where 1 point is about 0.4 kJ of round energy, so real calibres keep their proportions. player drone 200. rounds: 7.62 mm M134 8, .50 cal 45, 20 mm M24 and M61 140 (the M61 also bursts for 80 at the centre, falling to 0 at 6 m). missile 1800 on enemies (falloff to 25 percent at the radius), 120 on a drone. enemies: FPV drone 60 (shoots 8), tank 2500, worm segment 300, boss 40000 (turret rounds 35, missiles 120). `EnemyScript.MaxDamagePerRequest` (3000) must stay above the biggest single hit. a direct hit always deals full damage to the enemy struck, since a big target's centre is far from its surface

## Weapon system

* `WeaponScript` is a `Node3D` component placed as a child of `Mount` in the player scene. it owns: fire rate, projectile speed, damage, projectile prefab, shoot sound, and a `Barrel` (`Marker3D`) child as the spawn point
* the drone has three weapon slots (`Loadout`): 0 = `fire` (left mouse), 1 = `fire_missile` (right mouse), 2 = `fire_tertiary` (mouse button 5, `button_index` 9). `PlayerScript.BuildWeapons` instantiates the catalog prefab of each slot under `Mount` at `Loadout.Mounts[slot]`; the choice is made in the main menu (LOADOUT page) and saved in `user://loadout.cfg`. other peers read it from the replicated `PlayerScript.SyncLoadout` (packed, built lazily on the puppet). the slot index names a weapon across the network (`WeaponScript.Index`)
* `TryFire()` checks rate/ammo, calls `SpawnProjectile(origin, forward, mine: true)` and `NetSession.AnnounceShot`. `SpawnProjectile` is also what other peers call to replay a shot
* `ILaunchable` interface: `void Launch(Vector3 origin, Vector3 forward, float speed)` — implemented by both `BulletScript` and `MissileScript`. `BulletScript` gets special-cased first for the `damage` param, then falls through to `ILaunchable` for everything else
* projectile orientation: `SpawnProjectile` calls `projectile.LookAt(origin + forward, Vector3.Up)` after `AddChild` so the model faces the flight direction before `Launch` is called
* `BulletScript.Launch(origin, forward, owner, damage, speed)` — speed is a param, not an export. default 900 m/s. `Authoritative` (field, set before `Launch`) = this copy may damage enemies
* `MissileScript.Launch(origin, forward, forcedTarget, speed)` — speed sets the instance field used by homing physics too. default 100 m/s. `Authoritative` as above
* weapon prefabs (catalog in `Loadout.Catalog`): `weapon_50cal`, `weapon_minigun` (M134), `weapon_missile_launcher`, `weapon_dual20` (B-36 twin M24A1, two `Barrel1/2` markers fired in turn), `weapon_vulcan20` (M61, `splashRadius` makes `BulletScript` burst and damage everything in range; one in four bursts shows an explosion). `WeaponScript` exports `label` (HUD tag), `isMissile` (HUD icons), `shake`, `shootPitch`, `spread`; any child marker named `Barrel*` is a muzzle
* to add a new gun: create a prefab with `WeaponScript` + `Barrel` marker and add it to `Loadout.Catalog` (append only, the saved choices are indexes)
* ammo system: `WeaponScript` has `[Export] int maxAmmo` (0 = unlimited) and `[Export] float reloadTime`. reload is background — fires keep ticking on the active weapon while this one reloads. exposes `Ammo`, `MaxAmmo`, `IsReloading`, `ReloadProgress` for HUD use
* `PlayerScript` passes `_weapons` array to `_hud.Weapons` after discovering them — HUD reads weapon state each frame in `_Draw`
* `WeaponScript._timer` ticks in `_Process` every frame regardless of input — this ensures the first press always fires immediately

## Missile system

* `MissileScript.Launch(origin, forward, forcedTarget, speed)` — pass a `forcedTarget` to lock the seeker onto a specific node (e.g. a player) and skip `TryAcquireLock` entirely
* arming distance (default 4m) prevents self-detonation when launching at speed — collision detection is skipped until the missile has travelled that far
* `ApplySplash` damages the local player (`PlayerScript.Local`) if the missile is an enemy one (has a forced target) or the player's own authoritative one, and enemies in the groups only when `Authoritative`
* homing seeker searches `"targets"` group + `"worm_segments"` group — any `Node3D` in those groups is a valid lock
* flare seduction: `TrySeduceByFlare` runs each physics frame and overrides `_lockTarget` if a flare enters the seeker cone within `seekerRange`

## Flare system

* `FlareScript` adds itself to the `"flares"` group in `_Ready`. `MissileScript.TrySeduceByFlare` iterates this group and steals lock if a flare is in cone + range
* player fires flares with the `flare` input (X key), rate-limited by `flareRate` (default 0.4s) in `PlayerScript`; `Projectiles.SpawnFlare` locally plus `NetSession.AnnounceFlare` so every peer's missiles see it

## Performance gotchas

* every `new StandardMaterial3D` allocates a GPU descriptor — share static materials across instances for frequently-spawned objects (missiles, particles). D3D12 descriptor heap exhaustion shows as `uniform_set_create` errors
* `ImmediateMesh` rebuilt every frame per object is expensive in the transparent pass — avoid for frequently-spawned objects; use `GpuParticles3D` or pre-built meshes instead (bullet trails still do this)
* `SmokeParticleScript` shares a static `SphereMesh` but still creates one `StandardMaterial3D` per instance (needed for per-instance alpha fade). keep spawn rate and lifetime low to bound the count
* `Basis.Slerp` throws `InvalidOperationException` if the basis is not normalized — call `.Orthonormalized()` before `Slerp` on any basis that may have been affected by physics
* never create a Godot object per frame (queries, strings used as `StringName`, arrays): it leaks native memory until a full GC

## Health bar

* health bars use `prefabs/health_bar.tscn` (HealthBarScript) — a shared prefab instanced as a child named `HealthBar` in each enemy prefab
* `HealthBarScript` builds three `QuadMesh` children + one `Label3D` in code: dark background (priority 1), amber ghost (priority 2), green-to-red fill (priority 3), HP number label (priority 4)
* billboarding is done via `LookAt(camera.GlobalPosition, Vector3.Up)` on the parent `Node3D` in `_Process` (the camera is `GetViewport().GetCamera3D()`) — do NOT put `BillboardMode` on individual materials, they will desync at oblique angles
* after `LookAt`, call `RotateObjectLocal(Vector3.Up, Mathf.Pi)` to flip the quads toward the camera (`QuadMesh` faces +Z, `LookAt` points -Z at target)
* guard `LookAt` with a distance check (`> 0.1f`) and a cross-product check (`dir.Cross(Vector3.Up).LengthSquared() > 0.001f`) to avoid degenerate up-vector crashes
* `EnemyScript` calls `_healthBar?.Init(maxHealth)` after `OnReady()` and `_healthBar?.SetRatio(float)` whenever health changes (host: on damage, client: when `SyncHealth` changes)
* ghost effect: on damage, captures previous ratio as `_ghostRatio`, sets `_ghostAlpha = 1`, waits `GhostFadeDelay` (0.5s) then fades at `GhostFadeSpeed` (1.5/s). ghost quad sits between fill right edge and previous right edge
* fill quad X: center-anchored at origin, so right edge = `-width/2 + fillWidth`. ghost center = `fillRightEdge + ghostWidth/2`
* HP label: `Label3D`, right-aligned to bar's right edge, above the bar, colored to match fill (red→green via `Color.FromHsv`), no outline, `PixelSize = 0.012f`

## Enemy architecture

* `EnemyScript : Node3D` — base class for all enemies. owns: `maxHealth`, `explosionRadius`, `_health`, `_player` (the nearest drone, host only), `_healthBar`, `Sync*` properties, `Simulate(delta)`, `TakeDamage(int, Vector3)` (called by the local player's shots: feedback now, damage on the host), `SpawnExplosion`, `RandomizedFireTimer`
* subclasses override `OnReady()` (called before `_health = maxHealth`) to set `maxHealth`, `explosionRadius`, call `AddToGroup`, and do their own init; `OnDied()` (host, before the explosion); `FreedBySpawner` (false for things a script builds itself)
* `BossDroneScript : EnemyScript` — a 36 m quad-rotor gunship (`prefabs/boss_drone.tscn`, 400 health): the host flies it toward the nearest drone and fires 12 turrets (`Model/Turrets/TurretN`, each with a `Barrel` marker) and two homing missile pods (`MissileL/R`); every peer aims the turrets and spins the rotors for show. spawned once by `EnemySpawner` (`bossCount`), at least 600 m from the pad. each kill doubles the bosses alive (`OnDied` spawns `before + 1` through `SpawnBossReinforcements`, from just outside the border), capped at `EnemySpawner.MaxBosses` = 50
* `TankScript : EnemyScript` — ground enemy, fires missiles, moves toward the nearest drone, snaps to ground
* `DroneEnemyScript : EnemyScript` — floating target, fires bullets, rotates to face the nearest drone
* `WormSegmentScript : EnemyScript` — individual worm segment; `OnDied` notifies `Worm.OnSegmentDied`
* `WormScript : Node3D` — NOT an EnemyScript subclass; it has no health itself, only segments do. manages segment chain movement and firing (host) and replicates the chain
* bullet/missile hit detection casts to `EnemyScript` directly — adding a new enemy subclass gets damage handling for free as long as it's in the right group. a new enemy prefab needs a `Synchronizer` (`SyncPosition`, `SyncRotation` always, `SyncHealth` on change; copy `drone_enemy.tscn`) and a case in `EnemySpawner`
* enemies fire through `NetSession.Instance.EnemyBullet/EnemyMissile` (host only), never by instancing projectiles directly
* staggered spawn: always init fire timers with `RandomizedFireTimer(fireRate)` (returns `GD.Randf() * fireRate`) to avoid all enemies firing simultaneously on first frame

## Ground enemies (TankScript)

* tanks spawn via `EnemySpawner` using a downward raycast from a random XZ position to find terrain surface — `CollideWithAreas = false`,  `CollideWithBodies = true`
* tanks snap to ground each frame they move using the same raycast pattern
* `Basis.LookingAt` requires a non-zero, non-parallel direction — always compute `flatDir` from the XZ projection of `toPlayer` before calling it
* tank barrel node path: `Turret/Barrel` — cache it in `_Ready` to avoid `GetNode` exceptions mid-physics-process

## OnReady pattern (EnemyScript subclasses)

* `EnemyScript._Ready` runs setup in this order: `_healthBar` lookup → `OnReady()` → `_health = maxHealth` → `_healthBar.Init(maxHealth)` → (host) `SyncHealth = maxHealth`
* subclasses MUST set `maxHealth` and `explosionRadius` inside `OnReady()`, not at field declaration — field values are set before `_Ready` runs and will be overwritten
* do NOT call `base._Ready()` from subclasses — `EnemyScript._Ready` is sealed via the `OnReady` hook pattern

## Sounds folder

audio files live in `sounds/` . notable files:
* `49483__lorenzosu__helicopterraw_30sec.wav` — drone blades loop
* `653191__sebastianvdb__close-gun-shots-trim.mp3` — gun shot
* `hitmarker.mp3` — hitmarker
* `268557__cydon__explosion_001.mp3` — explosion (5.6s, use `MaxDistance=2000`,  `UnitSize=100`)
* `521377__jarusca__rocket-launch.mp3` — missile launch
* `620077__ryankingart__metal-impact-trim.mp3` — player hull impact

## Scene structure (player)

player prefab root: `Player` (RigidBody3D, PlayerScript); in the world it is spawned under `Players/<peerId>`
* `Mount` — camera + weapon mount, tilted slightly forward in local space
* `Mount/Camera3D` — main camera (freed on puppets)
* `Mount/Camera3D/CanvasLayer` — HUD canvas layer (used for damage numbers and HudScript)
* `Mount/Camera3D/CanvasLayer/Control/HealthLabel` — HP label
* `Mount/Weapon50Cal` — primary weapon (WeaponScript, index 0)
* `Mount/WeaponMissileLauncher` — secondary weapon (WeaponScript, index 1)
* `BladesAudio` — AudioStreamPlayer3D (drone rotor loop; puppets pitch it from their speed)
* `HitmarkerAudio` — AudioStreamPlayer
* `MetalImpactAudio` — AudioStreamPlayer (plays on collision and on TakeDamage)
* `Synchronizer` — MultiplayerSynchronizer (pose, owned by the flying peer)
* shoot audio is owned by each `WeaponScript` instance, not a top-level node

world scene (`main_scene.tscn`): terrain, `Players` + `PlayerSpawner`, `Enemies` + `EnemySpawner`

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

## HUD system (HudScript)

* `HudScript : Control` is instantiated in code by `PlayerScript` and added to `CanvasLayer`. it is NOT a scene file
* all HUD rendering is done via `_Draw` (Godot's immediate-mode canvas API) — no child `Label` nodes except for the pre-existing `HealthLabel` which lives outside `HudScript`, and the small top-left info panel (FPS, time, and `ROOM code players ping` when online)
* `QueueRedraw()` is called every `_Process` frame to keep the draw fresh
* `DrawString` with `HorizontalAlignment.Right` and a width param clips unreliably — prefer left-aligned at a computed X offset instead
* ammo widgets are drawn bottom-right, stacked upward. layout uses named constants (`AmmoLabelH`, `AmmoIconsY`, `AmmoBarY`, `AmmoWidgetH`) so the vertical zones are explicit and don't overlap
* icon grid: up to `AmmoMaxIcons` (10) icons per weapon. if `maxAmmo > 10`, each icon represents a chunk (`maxAmmo / icons` rounds). filled icons use `ColMain` (green), empty icons use dim alpha, low ammo (≤25%) turns red
* bullet icon: tall narrow rect + top cap. missile icon: triangle nose + rectangle body. both drawn filled or outlined depending on ammo state
* reload bar fills amber left-to-right as reload progresses; when full ammo it shows green proportional fill
* weapon name (`GUN`/`MSL`) and count (`48/50` or `RLDG`) are drawn on the same header row — name at x+4, count at x+40

## Known debt

* `Nullable` is not enabled and the older scripts are not nullable-clean
* tuning numbers live in scripts and prefab exports, not in `Resource` `.tres` files
* older scripts have no namespaces; no tests project; the Windows export is verified, the Linux export and the release workflow are not.
* bullet trails rebuild an `ImmediateMesh` per bullet per frame
