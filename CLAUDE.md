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

the drone should behave like an fpv drone with first person camera controls just like the battlefield helicopter control scheme, like this: W and S for throttle, A and D for yaw, mouse X for roll, mouse Y for pitch, no need to support controllers yet
