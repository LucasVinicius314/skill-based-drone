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
  }

  [Export] int count = 20;
  [Export] float radius = 100f;
  [Export] int wormCount = 3;
  [Export] int tankCount = 5;

  readonly PackedScene _dronePrefab = GD.Load<PackedScene>("res://prefabs/drone_enemy.tscn");
  readonly PackedScene _wormPrefab = GD.Load<PackedScene>("res://prefabs/worm.tscn");
  readonly PackedScene _tankPrefab = GD.Load<PackedScene>("res://prefabs/tank.tscn");

  int _nextId;

  public override void _Ready()
  {
    SpawnFunction = Callable.From<Variant, Node>(Build);
  }

  // the host calls this once its session is running: at once when solo, when the room exists when hosting
  public void SpawnAll()
  {
    for (var i = 0; i < count; i++)
    {
      Place(Kind.Drone, AboveGround(RandomOnSphere() * radius, 8f));
    }
    for (var i = 0; i < wormCount; i++)
    {
      Place(Kind.Worm, AboveGround(RandomOnSphere() * radius, 12f));
    }
    for (var i = 0; i < tankCount; i++)
    {
      var xz = RandomOnDisk() * radius;
      Place(Kind.Tank, new Vector3(xz.X, Terrain.HeightAt(xz.X, xz.Y) + 0.6f, xz.Y));
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
      _ => _dronePrefab,
    };

    var node = prefab.Instantiate<Node3D>();
    node.Name = $"Enemy{args[2].AsInt32()}";
    node.Position = args[1].AsVector3();
    return node;
  }

  // keeps a flying enemy at least `clearance` metres over the ground
  static Vector3 AboveGround(Vector3 position, float clearance)
  {
    position.Y = Mathf.Max(position.Y, Terrain.HeightAt(position.X, position.Z) + clearance);
    return position;
  }

  static Vector2 RandomOnDisk()
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    var angle = rng.RandfRange(0f, Mathf.Tau);
    var r = Mathf.Sqrt(rng.Randf());
    return new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
  }

  static Vector3 RandomOnSphere()
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    var v = new Vector3(rng.Randfn(), rng.Randfn(), rng.Randfn());
    return v.Normalized();
  }
}
