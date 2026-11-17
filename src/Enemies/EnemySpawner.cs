using Godot;

// host only: spawns the enemies, through a MultiplayerSpawner so every peer builds the same node from the same data
// (kind, position, id). the node name is the id, which the enemies' synchronizers need to match on every peer
public partial class EnemySpawner : MultiplayerSpawner
{
  enum Kind
  {
    Drone,
    Tank,
    Worm,
    Boss,
  }

  const float MapMargin = 0.8f;
  const float PadClearance = 180f;
  const float BossPadDistance = 600f;
  const float ReinforcementMargin = 250f;

  public const int MaxBosses = 50;

  [Export] int count = 40;
  [Export] int wormCount = 5;
  [Export] int tankCount = 12;
  [Export] int bossCount = 1;

  readonly PackedScene _dronePrefab = GD.Load<PackedScene>("res://prefabs/drone_enemy.tscn");
  readonly PackedScene _wormPrefab = GD.Load<PackedScene>("res://prefabs/worm.tscn");
  readonly PackedScene _bossPrefab = GD.Load<PackedScene>("res://prefabs/boss_drone.tscn");
  readonly PackedScene _tankPrefab = GD.Load<PackedScene>("res://prefabs/tank.tscn");

  int _nextId;

  public override void _Ready()
  {
    SpawnFunction = Callable.From<Variant, Node>(Build);
  }

  // the host calls this once its session is running: at once when solo, when the room exists when hosting
  public void SpawnAll()
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();

    for (var i = 0; i < count; i++)
    {
      var xz = FindSpot(rng, flat: false);
      var ground = Terrain.HeightAt(xz.X, xz.Y);
      Place(Kind.Drone, new Vector3(xz.X, ground + rng.RandfRange(10f, 60f), xz.Y));
    }
    for (var i = 0; i < wormCount; i++)
    {
      var xz = FindSpot(rng, flat: false);
      var ground = Terrain.HeightAt(xz.X, xz.Y);
      Place(Kind.Worm, new Vector3(xz.X, ground + rng.RandfRange(15f, 40f), xz.Y));
    }
    for (var i = 0; i < bossCount; i++)
    {
      var xz = FindSpot(rng, flat: false, minPad: BossPadDistance);
      Place(Kind.Boss, new Vector3(xz.X, Terrain.HeightAt(xz.X, xz.Y) + 90f, xz.Y));
    }
    for (var i = 0; i < tankCount; i++)
    {
      var xz = FindSpot(rng, flat: true);
      Place(Kind.Tank, new Vector3(xz.X, Terrain.HeightAt(xz.X, xz.Y) + 0.6f, xz.Y));
    }
  }

  // a random spot on the map: inside the mountain rim, away from the start pad and the structures, and for ground
  // units on gentle, low ground. the heightfield is a pure function, so no physics is needed
  static Vector2 FindSpot(RandomNumberGenerator rng, bool flat, float minPad = PadClearance)
  {
    var limit = Terrain.Half * MapMargin;
    var spot = Vector2.Zero;
    for (var attempt = 0; attempt < 200; attempt++)
    {
      spot = new Vector2(rng.RandfRange(-limit, limit), rng.RandfRange(-limit, limit));
      if (spot.DistanceTo(Terrain.PadCenter) < minPad || Structures.Blocked(spot.X, spot.Y, 15f))
      {
        continue;
      }
      if (!flat || IsGentle(spot))
      {
        break;
      }
    }
    return spot;
  }

  static bool IsGentle(Vector2 at)
  {
    var h = Terrain.HeightAt(at.X, at.Y);
    if (h > 25f)
    {
      return false;
    }
    const float step = 6f;
    var dx = Terrain.HeightAt(at.X + step, at.Y) - Terrain.HeightAt(at.X - step, at.Y);
    var dz = Terrain.HeightAt(at.X, at.Y + step) - Terrain.HeightAt(at.X, at.Y - step);
    return Mathf.Abs(dx) < 4f && Mathf.Abs(dz) < 4f;
  }

  // host only: bosses arriving from outside the map, at random points just past the border. they fly in by themselves
  public void SpawnBossReinforcements(int amount)
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    var edge = Terrain.Half + ReinforcementMargin;

    for (var i = 0; i < amount; i++)
    {
      var along = rng.RandfRange(-edge, edge);
      var side = rng.RandiRange(0, 3);
      var x = side < 2 ? (side == 0 ? -edge : edge) : along;
      var z = side < 2 ? along : (side == 2 ? -edge : edge);
      Place(Kind.Boss, new Vector3(x, Terrain.HeightAt(x, z) + 90f, z));
    }
  }

  void Place(Kind kind, Vector3 position)
  {
    Spawn(new Godot.Collections.Array { (int)kind, position, _nextId++ });
  }

  Node Build(Variant data)
  {
    var args = data.AsGodotArray();
    var kind = (Kind)args[0].AsInt32();
    var prefab = kind switch
    {
      Kind.Tank => _tankPrefab,
      Kind.Worm => _wormPrefab,
      Kind.Boss => _bossPrefab,
      _ => _dronePrefab,
    };

    var node = prefab.Instantiate<Node3D>();
    node.Name = $"Enemy{args[2].AsInt32()}";
    node.Position = args[1].AsVector3();
    return node;
  }
}
