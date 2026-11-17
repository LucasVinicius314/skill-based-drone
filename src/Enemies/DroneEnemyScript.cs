using Godot;

public partial class DroneEnemyScript : EnemyScript
{
  [Export] float fireRate = 2f;
  [Export] int damage = 8;

  float _fireTimer;
  Node3D[] _props;

  protected override void OnReady()
  {
    maxHealth = 60;
    explosionRadius = 3f;
    AddToGroup("targets");
    _fireTimer = RandomizedFireTimer(fireRate);

    _props = new Node3D[4];
    for (var i = 0; i < _props.Length; i++)
    {
      _props[i] = GetNode<Node3D>($"Model/Prop{i}");
    }
  }

  public override void _Process(double delta)
  {
    base._Process(delta);

    // the rotors spin, alternating direction like a real quad
    for (var i = 0; i < _props.Length; i++)
    {
      _props[i].RotateY((i % 2 == 0 ? 1f : -1f) * 60f * (float)delta);
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (!Simulate(delta))
    {
      return;
    }

    LookAt(_player.GlobalPosition, Vector3.Up);

    _fireTimer -= (float)delta;
    if (_fireTimer <= 0f)
    {
      _fireTimer = fireRate;
      ShootAtPlayer();
    }
  }

  void ShootAtPlayer()
  {
    var forward = AddSpread((_player.GlobalPosition - GlobalPosition).Normalized(), 0.2f);
    NetSession.Instance.EnemyBullet(GlobalPosition, forward, damage);
  }
}
