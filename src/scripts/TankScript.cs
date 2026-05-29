using Godot;

public partial class TankScript : EnemyScript
{
  [Export] float missileFireRate = 3f;
  [Export] float moveSpeed = 4f;
  [Export] float turnSpeed = 1.5f;
  [Export] float detectionRange = 300f;

  float _fireTimer;
  Node3D _barrel;

  readonly PackedScene _missilePrefab = GD.Load<PackedScene>("res://prefabs/missile.tscn");

  protected override void OnReady()
  {
    maxHealth = 8;
    explosionRadius = 4f;
    AddToGroup("targets");
    _barrel = GetNode<Node3D>("Turret/Barrel");
    _fireTimer = RandomizedFireTimer(missileFireRate);
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_player == null) return;

    var toPlayer = _player.GlobalPosition - GlobalPosition;
    var distToPlayer = toPlayer.Length();

    var flatDir = new Vector3(toPlayer.X, 0, toPlayer.Z).Normalized();
    var targetBasis = Basis.LookingAt(flatDir, Vector3.Up);
    GlobalTransform = new Transform3D(
      GlobalBasis.Orthonormalized().Slerp(targetBasis, turnSpeed * (float)delta),
      GlobalPosition
    );

    if (distToPlayer > 20f && distToPlayer < detectionRange)
    {
      GlobalPosition += flatDir * moveSpeed * (float)delta;
      SnapToGround();
    }

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
}
