using Godot;

public partial class MissileScript : Node3D
{
  [Export] float speed = 25f;
  [Export] float lifetime = 6f;

  float _elapsed;
  Vector3 _velocity;

  public void Launch(Vector3 origin, Vector3 forward)
  {
    GlobalPosition = origin;
    GlobalTransform = new Transform3D(
      GlobalTransform.Basis.Rotated(GlobalTransform.Basis.X, 0),
      origin
    );
    LookAt(origin + forward, Vector3.Up);
    _velocity = forward * speed;
    BuildMesh();
  }

  void BuildMesh()
  {
    var body = new MeshInstance3D();
    var bodyMesh = new CapsuleMesh { Radius = 0.07f, Height = 0.5f };
    body.Mesh = bodyMesh;
    body.RotationDegrees = new Vector3(90, 0, 0);
    var bodyMat = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.35f) };
    body.MaterialOverride = bodyMat;
    AddChild(body);

    var nose = new MeshInstance3D();
    var noseMesh = new SphereMesh { Radius = 0.07f, Height = 0.14f };
    nose.Mesh = noseMesh;
    nose.Position = new Vector3(0, 0, -0.25f);
    var noseMat = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.2f, 0.1f) };
    nose.MaterialOverride = noseMat;
    AddChild(nose);

    var finMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.25f) };
    for (var i = 0; i < 4; i++)
    {
      var fin = new MeshInstance3D();
      var finMesh = new BoxMesh { Size = new Vector3(0.01f, 0.12f, 0.14f) };
      fin.Mesh = finMesh;
      fin.MaterialOverride = finMat;
      fin.Position = new Vector3(0, 0, 0.18f);
      fin.RotationDegrees = new Vector3(0, 0, 45f * i);
      AddChild(fin);
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    _elapsed += (float)delta;
    if (_elapsed >= lifetime)
    {
      QueueFree();
      return;
    }

    var move = _velocity * (float)delta;
    var query = new PhysicsRayQueryParameters3D
    {
      From = GlobalPosition,
      To = GlobalPosition + move,
      CollideWithAreas = true,
      CollideWithBodies = true,
    };

    var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (result.Count > 0)
    {
      var collider = result["collider"].As<Node>();
      var hitPos = result["position"].AsVector3();
      (collider.GetParentOrNull<TargetScript>() ?? collider as TargetScript)?.TakeDamage(3, hitPos);
      (collider.GetParentOrNull<WormSegmentScript>() ?? collider as WormSegmentScript)?.TakeDamage(3);
      QueueFree();
      return;
    }

    GlobalPosition += move;
    LookAt(GlobalPosition + _velocity.Normalized(), Vector3.Up);
  }
}
