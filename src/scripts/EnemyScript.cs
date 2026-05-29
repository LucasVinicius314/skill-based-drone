using Godot;

public partial class EnemyScript : Node3D
{
  [Export] public int maxHealth = 5;
  [Export] public float explosionRadius = 3f;

  protected int _health;
  protected Node3D _healthBar;
  protected MeshInstance3D _healthBarFill;
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
    _healthBar = GetNodeOrNull<Node3D>("HealthBar");
    _healthBarFill = GetNodeOrNull<MeshInstance3D>("HealthBar/Fill");
    OnReady();
    _health = maxHealth;
  }

  protected virtual void OnReady() { }

  public virtual void TakeDamage(int amount, Vector3 hitPosition)
  {
    _health -= amount;
    _player?.PlayHitmarker();
    UpdateHealthBar();

    var dmg = _damageNumberPrefab.Instantiate<DamageNumberScript>();
    GetTree().CurrentScene.AddChild(dmg);
    dmg.Show(hitPosition + Vector3.Up * 0.5f, amount, _camera, _canvasLayer);

    if (_health <= 0)
    {
      SpawnExplosion(GlobalPosition);
      QueueFree();
    }
  }

  protected void UpdateHealthBar()
  {
    if (_healthBarFill == null) return;
    var ratio = (float)_health / maxHealth;
    var t = _healthBarFill.Transform;
    t.Origin = new Vector3((ratio - 1f) * 0.5f, 0, 0);
    _healthBarFill.Transform = t;
    _healthBarFill.Scale = new Vector3(ratio, 1, 1);
  }

  protected void SpawnExplosion(Vector3 position)
  {
    var explosion = _explosionPrefab.Instantiate<ExplosionScript>();
    explosion.Init(explosionRadius);
    GetTree().CurrentScene.AddChild(explosion);
    explosion.GlobalPosition = position;
  }

  protected float RandomizedFireTimer(float fireRate) => GD.Randf() * fireRate;
}
