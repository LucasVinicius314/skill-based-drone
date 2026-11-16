using Godot;

public partial class WeaponScript : Node3D
{
  [Export] public float fireRate = 0.15f;
  [Export] public float projectileSpeed = 900f;
  [Export] public int damage = 1;
  [Export] public PackedScene projectilePrefab;
  [Export] public AudioStream shootSound;
  [Export] public float shootVolumeDb = -20f;
  [Export] public int maxAmmo = 0;       // 0 = unlimited
  [Export] public float reloadTime = 0f; // seconds

  AudioStreamPlayer3D _audio;
  Node3D _barrel;
  float _timer;
  int _ammo;
  float _reloadTimer;
  bool _reloading;

  bool? _firesBullets;
  readonly PhysicsRayQueryParameters3D _predictQuery = new() { CollideWithAreas = false, CollideWithBodies = true };

  // position in the player's Mount, so other peers can name this weapon when they replay a shot
  public int Index { get; set; }

  public int Ammo => _ammo;
  public int MaxAmmo => maxAmmo;
  public bool IsReloading => _reloading;
  public float ReloadProgress => reloadTime > 0f ? 1f - (_reloadTimer / reloadTime) : 1f;

  public override void _Ready()
  {
    _barrel = GetNodeOrNull<Node3D>("Barrel");
    _ammo = maxAmmo;

    _audio = new AudioStreamPlayer3D { MaxDistance = 120f, Stream = shootSound, VolumeDb = shootVolumeDb };
    AddChild(_audio);
  }

  public override void _Process(double delta)
  {
    _timer -= (float)delta;

    if (_reloading)
    {
      _reloadTimer -= (float)delta;
      if (_reloadTimer <= 0f)
      {
        _ammo = maxAmmo;
        _reloading = false;
      }
    }
  }

  public bool TryFire()
  {
    if (_timer > 0f) return false;
    if (_reloading) return false;
    if (_barrel == null || projectilePrefab == null) return false;
    if (maxAmmo > 0 && _ammo <= 0)
    {
      StartReload();
      return false;
    }

    _timer = fireRate;
    if (maxAmmo > 0)
    {
      _ammo--;
      if (_ammo <= 0)
      {
        StartReload();
      }
    }

    var origin = _barrel.GlobalPosition;
    var forward = -_barrel.GlobalTransform.Basis.Z;
    SpawnProjectile(origin, forward, true);
    NetSession.Instance.AnnounceShot(Index, origin, forward);
    return true;
  }

  // mine = fired by the local player: only that copy damages enemies. other peers replay it for show
  public void SpawnProjectile(Vector3 origin, Vector3 forward, bool mine)
  {
    var projectile = projectilePrefab.Instantiate<Node3D>();
    if (projectile is MissileScript missile)
    {
      missile.Authoritative = mine;
    }
    GetTree().CurrentScene.AddChild(projectile);

    projectile.GlobalPosition = origin;
    projectile.LookAt(origin + forward, Vector3.Up);

    if (projectile is BulletScript bullet)
    {
      bullet.Authoritative = mine;
      bullet.Launch(origin, forward, BulletScript.Faction.Player, damage, projectileSpeed);
    }
    else if (projectile is ILaunchable launchable)
    {
      launchable.Launch(origin, forward, projectileSpeed);
    }

    _audio.GlobalPosition = origin;
    _audio.Play();
  }

  void StartReload()
  {
    if (reloadTime <= 0f || maxAmmo <= 0) return;
    _reloading = true;
    _reloadTimer = reloadTime;
  }

  // Traces the bullet's parabolic path in a few segments to find the impact point.
  // Balances accuracy (catches flying targets) with performance (only 10 raycasts).
  public Vector3? PredictImpact(World3D world, Rid excludeRid, float maxDist = 2000f)
  {
    if (_barrel == null || projectilePrefab == null) return null;

    if (_firesBullets == null)
    {
      var testInstance = projectilePrefab.Instantiate();
      _firesBullets = testInstance is BulletScript;
      testInstance.Free();
    }
    if (_firesBullets != true) return null;

    const int steps = 10;
    var timeToMax = maxDist / projectileSpeed;
    var stepTime = timeToMax / steps;

    var origin = _barrel.GlobalPosition;
    var forward = -_barrel.GlobalTransform.Basis.Z;

    var prev = origin;
    for (var i = 1; i <= steps; i++)
    {
      var t = i * stepTime;
      var next = origin + forward * (projectileSpeed * t) + new Vector3(0, -0.5f * 9.8f * t * t, 0);

      _predictQuery.From = prev;
      _predictQuery.To = next;
      if (_predictQuery.Exclude.Count == 0)
      {
        _predictQuery.Exclude = [excludeRid];
      }

      var result = world.DirectSpaceState.IntersectRay(_predictQuery);
      if (result.Count > 0)
      {
        return result["position"].AsVector3();
      }

      prev = next;
    }

    return prev;
  }
}
