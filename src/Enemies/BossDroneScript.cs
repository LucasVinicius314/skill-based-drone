using Godot;

// a huge quad-rotor gunship: slow, very tough, a dozen turrets and two missile pods. the host flies it and fires;
// every peer spins the rotors and aims the turrets at the nearest drone, which is only for show
public partial class BossDroneScript : EnemyScript
{
  const float Altitude = 70f;
  const float Standoff = 220f;
  const float Speed = 14f;
  const float TurnSpeed = 0.4f;
  const float GunRange = 450f;
  const float MissileRange = 700f;
  const float TurretFireRate = 1.1f;
  const float MissileFireRate = 7f;
  const int TurretDamage = 35; // a 20 mm-class round against a small drone

  AnimatableBody3D _body;
  Node3D[] _props;
  Node3D[] _turrets;
  Node3D[] _muzzles;
  float[] _turretTimers;
  Node3D[] _pods;
  PlayerScript _aim;
  float _aimTimer;
  float _missileTimer;
  int _nextPod;

  protected override void OnReady()
  {
    maxHealth = 40000;
    explosionRadius = 30f;
    AddToGroup("targets");
    AddToGroup("bosses");

    // the solid body follows the boss explicitly each physics tick: moving only the parent left it behind
    _body = GetNode<AnimatableBody3D>("Body");
    _body.TopLevel = true;
    _body.SyncToPhysics = true;

    _props = new Node3D[4];
    for (var i = 0; i < _props.Length; i++)
    {
      _props[i] = GetNode<Node3D>($"Model/Prop{i}");
    }

    var turrets = GetNode("Model/Turrets").GetChildren();
    _turrets = new Node3D[turrets.Count];
    _muzzles = new Node3D[turrets.Count];
    _turretTimers = new float[turrets.Count];
    for (var i = 0; i < turrets.Count; i++)
    {
      _turrets[i] = (Node3D)turrets[i];
      _muzzles[i] = _turrets[i].GetNode<Node3D>("Barrel");
      _turretTimers[i] = RandomizedFireTimer(TurretFireRate);
    }

    _pods = new[] { GetNode<Node3D>("MissileL"), GetNode<Node3D>("MissileR") };
    _missileTimer = MissileFireRate;
  }

  // host only: every kill doubles the bosses alive (the one that fell included), up to a cap alive at once. they come
  // in from outside the map
  protected override void OnDied()
  {
    var before = GetTree().GetNodesInGroup("bosses").Count; // this one is still in the group
    var alive = before - 1;
    var amount = Mathf.Min(before + 1, EnemySpawner.MaxBosses - alive); // alive + amount = 2 * before
    if (amount > 0)
    {
      GetTree().CurrentScene.GetNode<EnemySpawner>("EnemySpawner").SpawnBossReinforcements(amount);
    }
  }

  public override void _Process(double delta)
  {
    base._Process(delta);

    for (var i = 0; i < _props.Length; i++)
    {
      _props[i].RotateY((i % 2 == 0 ? 1f : -1f) * 40f * (float)delta);
    }

    _aimTimer -= (float)delta;
    if (_aimTimer <= 0f || !IsInstanceValid(_aim))
    {
      _aimTimer = 0.5f;
      _aim = PlayerScript.Nearest(GlobalPosition);
    }

    if (_aim == null)
    {
      return;
    }

    foreach (var turret in _turrets)
    {
      var to = _aim.GlobalPosition - turret.GlobalPosition;
      if (to.LengthSquared() > 1f && to.Normalized().Cross(Vector3.Up).LengthSquared() > 0.001f)
      {
        turret.LookAt(_aim.GlobalPosition, Vector3.Up);
      }
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    _body.GlobalTransform = GlobalTransform;
    if (!Simulate(delta))
    {
      return;
    }

    var dt = (float)delta;
    var toPlayer = _player.GlobalPosition - GlobalPosition;
    var flat = new Vector3(toPlayer.X, 0f, toPlayer.Z);
    var distance = flat.Length();

    var position = GlobalPosition;
    if (distance > 1f)
    {
      var heading = flat / distance;
      if (distance > Standoff)
      {
        position += heading * Speed * dt;
      }

      var facing = Basis.LookingAt(heading, Vector3.Up);
      GlobalTransform = new Transform3D(GlobalBasis.Orthonormalized().Slerp(facing, TurnSpeed * dt), position);
    }

    var wanted = Terrain.HeightAt(position.X, position.Z) + Altitude;
    position.Y = Mathf.Lerp(position.Y, wanted, 1f - Mathf.Exp(-0.8f * dt));
    GlobalPosition = position;

    var aim = _player.GlobalPosition;
    for (var i = 0; i < _turrets.Length; i++)
    {
      _turretTimers[i] -= dt;
      if (_turretTimers[i] > 0f || _muzzles[i].GlobalPosition.DistanceTo(aim) > GunRange)
      {
        continue;
      }

      _turretTimers[i] = TurretFireRate;
      var forward = AddSpread((aim - _muzzles[i].GlobalPosition).Normalized(), 1.2f);
      NetSession.Instance.EnemyBullet(_muzzles[i].GlobalPosition, forward, TurretDamage);
    }

    _missileTimer -= dt;
    if (_missileTimer <= 0f && distance < MissileRange)
    {
      _missileTimer = MissileFireRate;
      var pod = _pods[_nextPod++ % _pods.Length];
      NetSession.Instance.EnemyMissile(pod.GlobalPosition, (aim - pod.GlobalPosition).Normalized(), _player.PeerId);
    }
  }
}
