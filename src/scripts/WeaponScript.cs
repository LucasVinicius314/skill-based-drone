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

    var projectile = projectilePrefab.Instantiate<Node3D>();
    GetTree().CurrentScene.AddChild(projectile);

    var origin = _barrel.GlobalPosition;
    var forward = -_barrel.GlobalTransform.Basis.Z;
    projectile.GlobalPosition = origin;
    projectile.LookAt(origin + forward, Vector3.Up);

    if (projectile is BulletScript bullet)
    {
      bullet.Launch(origin, forward, BulletScript.Faction.Player, damage, projectileSpeed);
    }
    else if (projectile is ILaunchable launchable)
    {
      launchable.Launch(origin, forward, projectileSpeed);
    }

    _audio.GlobalPosition = _barrel.GlobalPosition;
    _audio.Play();
    return true;
  }

  void StartReload()
  {
    if (reloadTime <= 0f || maxAmmo <= 0) return;
    _reloading = true;
    _reloadTimer = reloadTime;
  }
}
