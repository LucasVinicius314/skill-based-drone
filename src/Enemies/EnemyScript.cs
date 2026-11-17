using Godot;

// base class for every enemy. the host runs the AI and owns health; clients show the replicated state (SyncPosition,
// SyncRotation, SyncHealth are replicated by the prefab's MultiplayerSynchronizer) and send damage requests to the host
public partial class EnemyScript : Node3D
{
  const int MaxDamagePerRequest = 3000;
  const float TargetRefreshSeconds = 0.5f;
  const float LateJoinSeconds = 1f;

  [Export] public int maxHealth = 5;
  [Export] public float explosionRadius = 3f;

  [Export] public Vector3 SyncPosition { get; set; }
  [Export] public Quaternion SyncRotation { get; set; } = Quaternion.Identity;

  // -1 until the host's first update arrives
  [Export] public int SyncHealth { get; set; } = -1;

  protected int _health;
  public int Health => _health;
  protected HealthBarScript _healthBar;

  // the drone this enemy is currently after: the nearest one. only set on the host (see Simulate)
  protected PlayerScript _player;

  readonly PackedScene _damageNumberPrefab = GD.Load<PackedScene>("res://prefabs/damage_number.tscn");
  readonly PackedScene _explosionPrefab = GD.Load<PackedScene>("res://prefabs/explosion_enemy.tscn");

  float _targetTimer;
  float _age;
  bool _placed;
  bool _dead;
  int _shownHealth = -1;

  public override void _Ready()
  {
    _healthBar = GetNodeOrNull<HealthBarScript>("HealthBar");
    AddOutline();
    OnReady();
    _health = maxHealth;
    _healthBar?.Init(maxHealth);
    if (Multiplayer.IsServer())
    {
      SyncHealth = maxHealth;
    }
  }

  static readonly ShaderMaterial _outline = new() { Shader = GD.Load<Shader>("res://shaders/outline.gdshader") };

  // a red edge around every model part, so enemies are easy to pick out (the health bar is left alone)
  void AddOutline()
  {
    foreach (var node in FindChildren("*", "MeshInstance3D", true, false))
    {
      var mesh = (MeshInstance3D)node;
      if (_healthBar == null || !_healthBar.IsAncestorOf(mesh))
      {
        mesh.MaterialOverlay = _outline;
      }
    }
  }

  protected virtual void OnReady() { }

  // called when the host's health reaches 0, before the explosion
  protected virtual void OnDied() { }

  // true when a spawner owns this node's lifetime (it is removed on every peer when the host frees it)
  protected virtual bool FreedBySpawner => true;

  // the start of every AI tick: only the host simulates, and it needs a target
  protected bool Simulate(double delta)
  {
    if (!Multiplayer.IsServer() || _dead)
    {
      return false;
    }

    _targetTimer -= (float)delta;
    if (_targetTimer <= 0f || !IsInstanceValid(_player))
    {
      _targetTimer = TargetRefreshSeconds;
      _player = PlayerScript.Nearest(GlobalPosition);
    }
    return _player != null;
  }

  public override void _Process(double delta)
  {
    _age += (float)delta;

    if (Multiplayer.IsServer())
    {
      SyncPosition = GlobalPosition;
      SyncRotation = GlobalTransform.Basis.GetRotationQuaternion();
      return;
    }

    FollowHost((float)delta);
  }

  void FollowHost(float delta)
  {
    if (SyncHealth >= 0 && SyncHealth != _shownHealth)
    {
      _shownHealth = SyncHealth;
      _healthBar?.SetRatio((float)SyncHealth / maxHealth);
      if (SyncHealth == 0 && !_dead)
      {
        Vanish(_age > LateJoinSeconds);
      }
    }

    if (_dead || SyncPosition == Vector3.Zero)
    {
      return;
    }

    if (!_placed || GlobalPosition.DistanceSquaredTo(SyncPosition) > 2500f)
    {
      _placed = true;
      GlobalTransform = new Transform3D(new Basis(SyncRotation), SyncPosition);
      return;
    }

    var follow = 1f - Mathf.Exp(-15f * delta);
    var basis = new Basis(GlobalTransform.Basis.GetRotationQuaternion().Slerp(SyncRotation, follow));
    GlobalTransform = new Transform3D(basis, GlobalPosition.Lerp(SyncPosition, follow));
  }

  // called by a shot that belongs to the local player. the damage counts on the host, the feedback is immediate here
  public virtual void TakeDamage(int amount, Vector3 hitPosition)
  {
    if (_dead)
    {
      return;
    }

    PlayerScript.Local?.PlayHitmarker();
    ShowDamageNumber(amount, hitPosition);

    if (Multiplayer.IsServer())
    {
      ApplyDamage(amount);
    }
    else
    {
      RpcId(1, MethodName.RpcRequestDamage, amount);
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcRequestDamage(int amount)
  {
    if (!Multiplayer.IsServer() || amount < 1 || amount > MaxDamagePerRequest)
    {
      return;
    }
    ApplyDamage(amount);
  }

  // host only
  void ApplyDamage(int amount)
  {
    if (_dead)
    {
      return;
    }

    _health = Mathf.Max(_health - amount, 0);
    SyncHealth = _health;
    _healthBar?.SetRatio((float)_health / maxHealth);

    if (_health <= 0)
    {
      OnDied();
      Vanish(true);

      // let the replicated health reach the clients (they explode on seeing it) before the node goes away
      GetTree().CreateTimer(0.5f).Timeout += () =>
      {
        if (IsInstanceValid(this))
        {
          QueueFree();
        }
      };
    }
  }

  // the enemy is gone: stop being a target or a collider, show the explosion
  void Vanish(bool explode)
  {
    _dead = true;
    foreach (var group in GetGroups())
    {
      RemoveFromGroup(group);
    }
    foreach (var area in FindChildren("*", "Area3D", true, false))
    {
      ((Area3D)area).CollisionLayer = 0;
    }

    Visible = false;
    if (explode)
    {
      SpawnExplosion(GlobalPosition);
    }

    // segments are not owned by a spawner, so nothing else will remove them on a client
    if (!Multiplayer.IsServer() && !FreedBySpawner)
    {
      QueueFree();
    }
  }

  void ShowDamageNumber(int amount, Vector3 hitPosition)
  {
    var local = PlayerScript.Local;
    if (local == null)
    {
      return;
    }

    var dmg = _damageNumberPrefab.Instantiate<DamageNumberScript>();
    GetTree().CurrentScene.AddChild(dmg);
    dmg.Show(hitPosition + Vector3.Up * 0.5f, amount, local.Camera, local.HudLayer);
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
