using Godot;

public partial class EnemyScript : Node3D
{
  [Export] public int maxHealth = 5;
  [Export] public float explosionRadius = 3f;

  protected int _health;
  protected HealthBarScript _healthBar;
  protected PlayerScript _player;
  protected Camera3D _camera;
  protected CanvasLayer _canvasLayer;

  readonly PackedScene _damageNumberPrefab = GD.Load<PackedScene>("res://prefabs/damage_number.tscn");
  readonly PackedScene _explosionPrefab = GD.Load<PackedScene>("res://prefabs/explosion_enemy.tscn");

  public override void _Ready()
  {
    _player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");
    _camera = _player.GetNode<Camera3D>("Mount/Camera3D");
    _canvasLayer = _player.GetNode<CanvasLayer>("Mount/Camera3D/CanvasLayer");
    _healthBar = GetNodeOrNull<HealthBarScript>("HealthBar");
    OnReady();
    _health = maxHealth;
    _healthBar?.Init(maxHealth);
  }

  protected virtual void OnReady() { }

  public virtual void TakeDamage(int amount, Vector3 hitPosition)
  {
    _health -= amount;
    _player?.PlayHitmarker();
    _healthBar?.SetRatio((float)_health / maxHealth);

    var dmg = _damageNumberPrefab.Instantiate<DamageNumberScript>();
    GetTree().CurrentScene.AddChild(dmg);
    dmg.Show(hitPosition + Vector3.Up * 0.5f, amount, _camera, _canvasLayer);

    if (_health <= 0)
    {
      SpawnExplosion(GlobalPosition);
      QueueFree();
    }
  }

  protected void SpawnExplosion(Vector3 position)
  {
    var explosion = _explosionPrefab.Instantiate<ExplosionScript>();
    explosion.Init(explosionRadius);
    GetTree().CurrentScene.AddChild(explosion);
    explosion.GlobalPosition = position;
  }

  protected float RandomizedFireTimer(float fireRate) => GD.Randf() * fireRate;

  public static Vector3 AddSpread(Vector3 forward, float angleDeg)
  {
    var rng = new RandomNumberGenerator();
    var axis = forward.Cross(Vector3.Up).Normalized();
    if (axis.LengthSquared() < 0.001f)
    {
      axis = forward.Cross(Vector3.Right).Normalized();
    }
    axis = axis.Rotated(forward, rng.RandfRange(0f, Mathf.Tau));
    return forward.Rotated(axis, Mathf.DegToRad(rng.RandfRange(0f, angleDeg))).Normalized();
  }
}
