using Godot;

public partial class WormSegmentScript : Node3D
{
  [Export] public int MaxHealth = 2;

  public int Health;
  public WormScript Worm;

  PlayerScript _player;
  readonly PackedScene _explosionPrefab = GD.Load<PackedScene>("res://prefabs/explosion_enemy.tscn");

  public override void _Ready()
  {
    Health = MaxHealth;
    AddToGroup("worm_segments");
    _player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");
  }

  public void TakeDamage(int amount)
  {
    _player?.PlayHitmarker();
    Health -= amount;
    if (Health <= 0)
    {
      SpawnExplosion(GlobalPosition);
      Worm?.OnSegmentDied(this);
      QueueFree();
    }
  }

  void SpawnExplosion(Vector3 position)
  {
    var explosion = _explosionPrefab.Instantiate<ExplosionScript>();
    explosion.Init(2f);
    GetTree().CurrentScene.AddChild(explosion);
    explosion.GlobalPosition = position;
  }

  public void SetColor(Color color)
  {
    var mat = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = mat;
  }
}
