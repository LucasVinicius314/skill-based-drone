using Godot;

public partial class PlayerScript : RigidBody3D
{
  [Export] float throttleForce = 20f;
  [Export] float yawTorque = 3f;
  [Export] float pitchTorque = 3f;
  [Export] float rollTorque = 3f;
  [Export] float mouseSensitivity = 0.003f;
  [Export] float fireRate = 0.08f;
  [Export] float fovDefault = 120f;
  [Export] float fovMin = 20f;
  [Export] float fovMax = 120f;
  [Export] float fovScrollStep = 5f;
  [Export] float fovLerpSpeed = 10f;

  float _targetFov;
  float _pitchInput;
  float _rollInput;
  float _fireTimer;

  [Export] float bladePitchIdle = 0.8f;
  [Export] float bladePitchMax = 1.4f;

  PackedScene _bulletPrefab = GD.Load<PackedScene>("res://prefabs/bullet.tscn");
  Node3D _barrel;
  Camera3D _camera;
  AudioStreamPlayer3D _bladesAudio;
  AudioStreamPlayer3D _shootAudio;
  AudioStreamPlayer _hitmarkerAudio;

  public override void _Ready()
  {
    Input.MouseMode = Input.MouseModeEnum.Captured;
    _barrel = GetNode<Node3D>("Node3D/Barrel");
    _camera = GetNode<Camera3D>("Node3D/Camera3D");
    _bladesAudio = GetNode<AudioStreamPlayer3D>("BladesAudio");
    _shootAudio = GetNode<AudioStreamPlayer3D>("ShootAudio");
    _hitmarkerAudio = GetNode<AudioStreamPlayer>("HitmarkerAudio");
    _bladesAudio.Stream = GD.Load<AudioStream>("res://sounds/49483__lorenzosu__helicopterraw_30sec.wav");
    _shootAudio.Stream = GD.Load<AudioStream>("res://sounds/653191__sebastianvdb__close-gun-shots-trim.mp3");
    _hitmarkerAudio.Stream = GD.Load<AudioStream>("res://sounds/hitmarker.mp3");
    _camera.Fov = fovDefault;
    _targetFov = fovDefault;
    _bladesAudio.Finished += () => _bladesAudio.Play();
    _bladesAudio.Play();
    _health = maxHealth;
    _healthLabel = GetNode<Label>("Node3D/Camera3D/CanvasLayer/Control/HealthLabel");
    UpdateHealthLabel();
  }

  public void PlayHitmarker()
  {
    _hitmarkerAudio.Play();
  }

  public void TakeDamage(int amount)
  {
    _health = Mathf.Max(_health - amount, 0);
    UpdateHealthLabel();

    var rng = new RandomNumberGenerator();
    ApplyTorque(new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)).Normalized() * hitSpinForce);

    if (_health <= 0)
    {
      GD.Print("Player dead");
    }
  }

  void UpdateHealthLabel()
  {
    _healthLabel.Text = $"HP: {_health}";
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventMouseMotion motion)
    {
      _rollInput = motion.Relative.X;
      _pitchInput = motion.Relative.Y;
    }

    if (@event is InputEventMouseButton scroll && scroll.Pressed)
    {
      if (scroll.ButtonIndex == MouseButton.WheelUp)
      {
        _targetFov = Mathf.Clamp(_targetFov - fovScrollStep, fovMin, fovMax);
      }
      else if (scroll.ButtonIndex == MouseButton.WheelDown)
      {
        _targetFov = Mathf.Clamp(_targetFov + fovScrollStep, fovMin, fovMax);
      }
    }

    if (@event is InputEventKey key && key.Pressed)
    {
      if (key.Keycode == Key.Escape)
      {
        Input.MouseMode = Input.MouseModeEnum.Visible;
      }
      else if (key.Keycode == Key.R)
      {
        GetTree().ReloadCurrentScene();
      }
    }
  }

  [Export] float hitSpinForce = 8f;
  [Export] int maxHealth = 10;

  int _health;
  Label _healthLabel;

  public int Health => _health;

  public override void _PhysicsProcess(double delta)
  {
    var throttle = Input.GetAxis("throttle_up", "throttle_down");   // S = -1, W = +1
    var yaw = Input.GetAxis("yaw_up", "yaw_down");     // D = -1, A = +1

    // Throttle: push up along the drone's local Y axis
    ApplyCentralForce(-GlobalTransform.Basis.Y * throttle * throttleForce);

    // Yaw: rotate around local Y axis
    ApplyTorque(GlobalTransform.Basis.Y * yaw * yawTorque);

    // Roll: mouse X + roll axis rotates around local Z axis
    var rollAxis = -Input.GetAxis("roll_up", "roll_down") * .25f;
    ApplyTorque(-GlobalTransform.Basis.Z * (_rollInput * mouseSensitivity + rollAxis) * rollTorque);

    // Pitch: mouse Y + pitch axis rotates around local X axis
    var pitchAxis = Input.GetAxis("pitch_up", "pitch_down") * .25f;
    ApplyTorque(GlobalTransform.Basis.X * (_pitchInput * mouseSensitivity + pitchAxis) * pitchTorque);

    _rollInput = 0f;
    _pitchInput = 0f;

    _bladesAudio.PitchScale = Mathf.Lerp(bladePitchIdle, bladePitchMax, Mathf.Abs(throttle));
    _camera.Fov = Mathf.Lerp(_camera.Fov, _targetFov, fovLerpSpeed * (float)delta);

    _fireTimer -= (float)delta;
    if (Input.GetActionStrength("fire") > 0.5f && _fireTimer <= 0f)
    {
      _fireTimer = fireRate;
      var bullet = _bulletPrefab.Instantiate<BulletScript>();
      GetTree().CurrentScene.AddChild(bullet);
      bullet.Launch(_barrel.GlobalPosition, -_barrel.GlobalTransform.Basis.Z);
      _shootAudio.Play();
    }
  }
}
