using Godot;

public partial class TargetScript : Node3D
{
  public override void _Ready()
  {
    GetNode<Area3D>("Area3D").BodyEntered += OnBodyEntered;
  }

  public void OnBodyEntered(Node3D body)
  {
    if (body is BulletScript)
    {
      body.QueueFree();
      QueueFree();
    }
  }
}
