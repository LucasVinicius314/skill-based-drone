using Godot;
using System.Linq;

// scripted checks, active only with dev flags (after `--`):
//   --net-host / --net-join=CODE / --solo / --net-name=NAME / --net-url=URL / --net-no-friends   start without the menu
//   --dev-pos=x,y,z,yawDeg  put the drone there at the start (to look at the terrain)
//   --net-tpv               switch to the third person camera (to see the other drones)
//   --net-shoot-at=SEC      fire 30 shots at the nearest enemy, SEC s into the world
//   --net-report-at=SEC     print `NET REPORT ...` lines (compare both sides), once
//   --screenshot=FILE       save the screen (at --screenshot-at=SEC, default 4), then quit
//   --menu-screenshot=FILE  save the main menu (--menu-page=host|join|settings picks the page), then quit
//   --net-quit-at=SEC       quit SEC s into the world
// time counts from when the local drone exists, so it does not depend on how long the connection took
public partial class NetDevHooks : Node
{
  double _elapsed;
  double _menuElapsed;
  int _shotsLeft;
  bool _shot;
  bool _tpv;
  bool _placed;
  bool _reported;
  bool _screenshotTaken;

  public override void _Process(double delta)
  {
    var player = PlayerScript.Local;
    if (player == null)
    {
      MenuScreenshot();
      return;
    }
    _elapsed += delta;

    if (!_placed && DevArgs.Get("dev-pos") is { } pos && _elapsed > 0.3)
    {
      _placed = true;
      var v = pos.Split(',');
      var at = new Vector3(float.Parse(v[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(v[2], System.Globalization.CultureInfo.InvariantCulture));
      player.GlobalTransform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(float.Parse(v[3], System.Globalization.CultureInfo.InvariantCulture))), at);
      player.LinearVelocity = Vector3.Zero;
    }

    if (!_tpv && DevArgs.Has("net-tpv") && _elapsed > 1)
    {
      _tpv = true;
      Input.ParseInputEvent(new InputEventKey { Keycode = Key.V, Pressed = true });
    }

    if (!_shot && DevArgs.GetInt("net-shoot-at") is { } shootAt && _elapsed > shootAt)
    {
      _shot = true;
      _shotsLeft = 30;
    }
    if (_shotsLeft > 0)
    {
      _shotsLeft--;
      Shoot(player);
    }

    if (!_reported && DevArgs.GetInt("net-report-at") is { } reportAt && _elapsed > reportAt)
    {
      _reported = true;
      Report();
    }

    if (!_screenshotTaken && DevArgs.Get("screenshot") is { } file && _elapsed > (DevArgs.GetInt("screenshot-at") ?? 4))
    {
      _screenshotTaken = true;
      var error = GetViewport().GetTexture().GetImage().SavePng(file);
      GD.Print($"SCREENSHOT {file} {error}");
      GetTree().Quit(0);
    }

    if (DevArgs.GetInt("net-quit-at") is { } quit && _elapsed > quit)
    {
      GD.Print("NET QUIT");
      GetTree().Quit(0);
    }
  }

  void MenuScreenshot()
  {
    if (DevArgs.Get("menu-screenshot") is not { } file || GetTree().CurrentScene is not MainMenu menu)
    {
      return;
    }

    _menuElapsed += GetProcessDeltaTime();
    if (_menuElapsed > 1.5)
    {
      menu.Call(MainMenu.MethodName.ShowPageForDev, DevArgs.Get("menu-page") ?? "main");
    }
    if (_menuElapsed > 2.5)
    {
      GetViewport().GetTexture().GetImage().SavePng(file);
      GD.Print($"SCREENSHOT {file}");
      GetTree().Quit(0);
    }
  }

  // the same path a real shot takes, aimed straight at the nearest enemy
  static void Shoot(PlayerScript player)
  {
    EnemyScript nearest = null;
    var best = float.MaxValue;
    foreach (var group in new[] { "targets", "worm_segments" })
    {
      foreach (var node in player.GetTree().GetNodesInGroup(group))
      {
        var distance = player.GlobalPosition.DistanceSquaredTo(((Node3D)node).GlobalPosition);
        if (node is EnemyScript enemy && distance < best)
        {
          best = distance;
          nearest = enemy;
        }
      }
    }

    var weapon = player.Weapon(0);
    if (nearest == null || weapon == null)
    {
      return;
    }

    var forward = (nearest.GlobalPosition - player.GlobalPosition).Normalized();
    var origin = player.GlobalPosition + forward * 3f;
    weapon.SpawnProjectile(origin, forward, true);
    NetSession.Instance.AnnounceShot(0, origin, forward);
  }

  void Report()
  {
    var session = NetSession.Instance;
    var tree = GetTree();
    GD.Print($"NET REPORT kind={session.Kind} code={session.RoomCode} roster={session.Roster.Count}");

    foreach (var node in tree.CurrentScene.GetNode("Players").GetChildren().OfType<PlayerScript>().OrderBy(p => p.Name.ToString()))
    {
      var pos = node.GlobalPosition;
      GD.Print($"NET REPORT player {node.Name} local={node.IsLocal} pos=({pos.X:0},{pos.Y:0},{pos.Z:0})");
    }

    var enemies = tree.GetNodesInGroup("targets").Concat(tree.GetNodesInGroup("worm_segments")).OfType<EnemyScript>();
    GD.Print($"NET REPORT enemies alive={enemies.Count()} damaged={enemies.Count(e => e.SyncHealth >= 0 && e.SyncHealth < e.maxHealth)}");
  }
}
