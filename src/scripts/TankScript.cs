using Godot;

public partial class TankScript : Node3D
{
  [Export] int maxHealth = 8;
  [Export] float missileFireRate = 3f;
  [Export] float moveSpeed = 4f;
  [Export] float turnSpeed = 1.5f;
  [Export] float detectionRange = 300f;

  int _health;
  float _fireTimer;
  MeshInstance3D _healthBarFill;
  Node3D _healthBar;
  Node3D _barrel;
  PlayerScript _player;
  Camera3D _camera;
  CanvasLayer _canvasLayer;

  readonly PackedScene _damageNumberPrefab = GD.Load<PackedScene>("res://prefabs/damage_number.tscn");
  readonly PackedScene _missilePrefab = GD.Load<PackedScene>("res://prefabs/missile.tscn");
  readonly PackedScene _explosionPrefab = GD.Load<PackedScene>("res://prefabs/explosion_enemy.tscn");

  public override void _Ready()
  {
    _health = maxHealth;
    AddToGroup("targets");
    _healthBar = GetNode<Node3D>("HealthBar");
    _barrel = GetNode<Node3D>("Turret/Barrel");
    _healthBarFill = GetNode<MeshInstance3D>("HealthBar/Fill");
    _player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");
    _camera = _player.GetNode<Camera3D>("Mount/Camera3D");
    _canvasLayer = _player.GetNode<CanvasLayer>("Mount/Camera3D/CanvasLayer");
    _fireTimer = missileFireRate;
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_player == null) return;

    var toPlayer = _player.GlobalPosition - GlobalPosition;
    var distToPlayer = toPlayer.Length();

    // rotate body to face player (yaw only, stay flat on ground)
    var flatDir = new Vector3(toPlayer.X, 0, toPlayer.Z).Normalized();
    var targetBasis = Basis.LookingAt(flatDir, Vector3.Up);
    GlobalTransform = new Transform3D(
      GlobalBasis.Orthonormalized().Slerp(targetBasis, turnSpeed * (float)delta),
      GlobalPosition
    );

    // move toward player, stop when close
    if (distToPlayer > 20f && distToPlayer < detectionRange)
    {
      GlobalPosition += flatDir * moveSpeed * (float)delta;
      SnapToGround();
    }

    // billboard health bar toward camera
    _healthBar.LookAt(_camera.GlobalPosition, Vector3.Up);

    _fireTimer -= (float)delta;
    if (_fireTimer <= 0f && distToPlayer < detectionRange)
    {
      _fireTimer = missileFireRate;
      FireMissile();
    }
  }

  void SnapToGround()
  {
    var spaceState = GetWorld3D().DirectSpaceState;
    var query = new PhysicsRayQueryParameters3D
    {
      From = GlobalPosition + Vector3.Up * 5f,
      To = GlobalPosition + Vector3.Down * 50f,
      CollideWithBodies = true,
      CollideWithAreas = false,
    };
    var result = spaceState.IntersectRay(query);
    if (result.Count > 0)
    {
      GlobalPosition = result["position"].AsVector3() + Vector3.Up * 0.6f;
    }
  }

  void FireMissile()
  {
    var missile = _missilePrefab.Instantiate<MissileScript>();
    GetTree().CurrentScene.AddChild(missile);
    missile.Launch(_barrel.GlobalPosition, -_barrel.GlobalTransform.Basis.Z, _player);
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
      SpawnExplosion(GlobalPosition);
      QueueFree();
    }
  }

  void SpawnExplosion(Vector3 position)
  {
    var explosion = _explosionPrefab.Instantiate<ExplosionScript>();
    explosion.Init(4f);
    GetTree().CurrentScene.AddChild(explosion);
    explosion.GlobalPosition = position;
  }
}
