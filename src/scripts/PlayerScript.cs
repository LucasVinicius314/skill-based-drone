using Godot;

public partial class PlayerScript : RigidBody3D
{
  [Export] float throttleForce = 20f;
  [Export] float yawTorque = 3f;
  [Export] float pitchTorque = 3f;
  [Export] float rollTorque = 3f;
  [Export] float mouseSensitivity = 0.003f;

  float _pitchInput;
  float _rollInput;

  public override void _Ready()
  {
    Input.MouseMode = Input.MouseModeEnum.Captured;
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseMotion motion)
    {
      _rollInput = motion.Relative.X;
      _pitchInput = motion.Relative.Y;
    }

    if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed)
    {
      Input.MouseMode = Input.MouseModeEnum.Visible;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    var throttle = Input.GetAxis("throttle_up", "throttle_down");   // S = -1, W = +1
    var yaw = Input.GetAxis("yaw_up", "yaw_down");     // D = -1, A = +1

    // Throttle: push up along the drone's local Y axis
    ApplyCentralForce(-GlobalTransform.Basis.Y * throttle * throttleForce);

    // Yaw: rotate around world Y axis
    ApplyTorque(Vector3.Up * yaw * yawTorque);

    // Roll: mouse X rotates around local Z axis
    ApplyTorque(-GlobalTransform.Basis.Z * _rollInput * mouseSensitivity * rollTorque);

    // Pitch: mouse Y rotates around local X axis
    ApplyTorque(GlobalTransform.Basis.X * _pitchInput * mouseSensitivity * pitchTorque);

    _rollInput = 0f;
    _pitchInput = 0f;
  }
}
