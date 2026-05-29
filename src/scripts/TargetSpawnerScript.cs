using Godot;

public partial class TargetSpawnerScript : Node3D
{
  [Export] int count = 20;
  [Export] float radius = 100f;
  [Export] int wormCount = 3;
  [Export] int tankCount = 5;

  readonly PackedScene _targetPrefab = GD.Load<PackedScene>("res://prefabs/drone_enemy.tscn");
  readonly PackedScene _wormPrefab = GD.Load<PackedScene>("res://prefabs/worm.tscn");
  readonly PackedScene _tankPrefab = GD.Load<PackedScene>("res://prefabs/tank.tscn");

  public override void _Ready()
  {
    for (var i = 0; i < count; i++)
    {
      SpawnTarget();
    }
    for (var i = 0; i < wormCount; i++)
    {
      SpawnWorm();
    }
    for (var i = 0; i < tankCount; i++)
    {
      SpawnTank();
    }
  }

  void SpawnWorm()
  {
    var worm = _wormPrefab.Instantiate<WormScript>();
    AddChild(worm);
    worm.GlobalPosition = RandomOnSphere() * radius;
  }

  void SpawnTarget()
  {
    var target = _targetPrefab.Instantiate<DroneEnemyScript>();
    AddChild(target);
    target.GlobalPosition = RandomOnSphere() * radius;
  }

  void SpawnTank()
  {
    var tank = _tankPrefab.Instantiate<TankScript>();
    AddChild(tank);
    var xz = RandomOnDisk() * radius;
    var groundPos = SnapToGround(new Vector3(xz.X, 0, xz.Y));
    tank.GlobalPosition = groundPos;
  }

  // returns the ground position below the given XZ point, or a fallback
  Vector3 SnapToGround(Vector3 xzPos)
  {
    var spaceState = GetWorld3D().DirectSpaceState;
    var query = new PhysicsRayQueryParameters3D
    {
      From = xzPos + Vector3.Up * 500f,
      To = xzPos + Vector3.Down * 500f,
      CollideWithBodies = true,
      CollideWithAreas = false,
    };
    var result = spaceState.IntersectRay(query);
    if (result.Count > 0)
    {
      return result["position"].AsVector3() + Vector3.Up * 0.6f;
    }
    return xzPos + Vector3.Up * 0.6f;
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
