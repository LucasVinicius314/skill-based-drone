using Godot;

public partial class BulletScript : RigidBody3D
{
  [Export] float speed = 80f;
  [Export] float lifetime = 3f;

  float _elapsed;

  public void Launch(Vector3 origin, Vector3 forward)
  {
    GlobalPosition = origin;
    LinearVelocity = forward * speed;
  }

  public override void _PhysicsProcess(double delta)
  {
    _elapsed += (float)delta;
    if (_elapsed >= lifetime)
    {
      QueueFree();
    }
  }
}
