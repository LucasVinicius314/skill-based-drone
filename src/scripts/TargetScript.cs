using Godot;

public partial class TargetScript : Node3D
{
  [Export] int maxHealth = 3;
  [Export] float fireRate = 2f;
  [Export] int damage = 1;

  int _health;
  float _fireTimer;
  MeshInstance3D _healthBarFill;
  PlayerScript _player;
  Camera3D _camera;
  CanvasLayer _canvasLayer;

  readonly PackedScene _damageNumberPrefab = GD.Load<PackedScene>("res://prefabs/damage_number.tscn");
  readonly PackedScene _bulletPrefab = GD.Load<PackedScene>("res://prefabs/bullet.tscn");

  public override void _Ready()
  {
    _health = maxHealth;
    AddToGroup("targets");
    _healthBarFill = GetNode<MeshInstance3D>("HealthBar/Fill");
    _player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");
    _camera = _player.GetNode<Camera3D>("Mount/Camera3D");
    _canvasLayer = _player.GetNode<CanvasLayer>("Mount/Camera3D/CanvasLayer");
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_player == null) return;

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
    var forward = (_player.GlobalPosition - GlobalPosition).Normalized();
    var bullet = _bulletPrefab.Instantiate<BulletScript>();
    GetTree().CurrentScene.AddChild(bullet);
    bullet.Launch(GlobalPosition, forward, BulletScript.Faction.Enemy);
  }

  public void TakeDamage(int amount, Vector3 hitPosition)
  {
    _health -= amount;
    _player?.PlayHitmarker();

    var t = _healthBarFill.Transform;
    t.Origin = new Vector3(((float)_health / maxHealth - 1f) * 0.5f, 0, 0);
    _healthBarFill.Transform = t;
    _healthBarFill.Scale = new Vector3((float)_health / maxHealth, 1, 1);

    var dmg = _damageNumberPrefab.Instantiate<DamageNumberScript>();
    GetTree().CurrentScene.AddChild(dmg);
    dmg.Show(hitPosition + Vector3.Up * 0.5f, amount, _camera, _canvasLayer);

    if (_health <= 0)
    {
      QueueFree();
    }
  }
}
