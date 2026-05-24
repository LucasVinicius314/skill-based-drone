using Godot;

public partial class WormSegmentScript : Node3D
{
  [Export] public int MaxHealth = 2;

  public int Health;
  public WormScript Worm;

  PlayerScript _player;

  public override void _Ready()
  {
    Health = MaxHealth;
    _player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");
  }

  public void TakeDamage(int amount)
  {
    _player?.PlayHitmarker();
    Health -= amount;
    if (Health <= 0)
    {
      Worm?.OnSegmentDied(this);
      QueueFree();
    }
  }

  public void SetColor(Color color)
  {
    var mat = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = mat;
  }
}
