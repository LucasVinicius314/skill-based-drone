using Godot;

public partial class TargetSpawnerScript : Node3D
{
  [Export] int count = 20;
  [Export] float radius = 100f;

  [Export] int wormCount = 3;

  readonly PackedScene _targetPrefab = GD.Load<PackedScene>("res://prefabs/target.tscn");
  readonly PackedScene _wormPrefab = GD.Load<PackedScene>("res://prefabs/worm.tscn");

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
  }

  void SpawnWorm()
  {
    var worm = _wormPrefab.Instantiate<WormScript>();
    AddChild(worm);
    worm.GlobalPosition = RandomOnSphere() * radius;
  }

  void SpawnTarget()
  {
    var target = _targetPrefab.Instantiate<TargetScript>();
    AddChild(target);
    target.GlobalPosition = RandomOnSphere() * radius;
  }

  Vector3 RandomOnSphere()
  {
    var rng = new RandomNumberGenerator();
    rng.Randomize();
    // uniform sphere sampling via normal distribution
    var v = new Vector3(rng.Randfn(), rng.Randfn(), rng.Randfn());
    return v.Normalized();
  }
}
