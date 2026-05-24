using Godot;

public partial class BulletScript : Node3D
{
  [Export] float speed = 80f;
  [Export] float lifetime = 3f;

  float _elapsed;
  Vector3 _velocity;

  public void Launch(Vector3 origin, Vector3 forward)
  {
    GlobalPosition = origin;
    _velocity = forward * speed;
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
      CollideWithBodies = false,
    };

    var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (result.Count > 0)
    {
      var collider = result["collider"].As<Node>();
      var target = collider.GetParentOrNull<TargetScript>();
      if (target != null)
      {
        target.QueueFree();
      }
      QueueFree();
      return;
    }

    GlobalPosition += move;
  }
}
