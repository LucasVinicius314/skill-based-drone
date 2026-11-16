using Godot;

public partial class DroneEnemyScript : EnemyScript
{
  [Export] float fireRate = 2f;
  [Export] int damage = 1;

  float _fireTimer;

  protected override void OnReady()
  {
    maxHealth = 3;
    explosionRadius = 3f;
    AddToGroup("targets");
    _fireTimer = RandomizedFireTimer(fireRate);
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
