using Godot;
using System.Linq;

public partial class PlayerScript : RigidBody3D
{
  [Export] float throttleForce = 20f;
  [Export] float yawTorque = 3f;
  [Export] float pitchTorque = 3f;
  [Export] float rollTorque = 3f;
  [Export] float mouseSensitivity = 0.003f;
  [Export] float fovDefault = 120f;
  [Export] float fovMin = 5f;
  [Export] float fovMax = 120f;
  [Export] float fovScrollStep = 5f;
  [Export] float fovLerpSpeed = 10f;
  [Export] float bladePitchIdle = 0.8f;
  [Export] float bladePitchMax = 1.4f;
  [Export] float flareRate = 0.4f;
  [Export] float hitSpinForce = 8f;
  [Export] int maxHealth = 10;

  float _targetFov;
  float _pitchInput;
  float _rollInput;
  float _flareTimer;
  float _lockOnBeepTimer;
  float _lockOnPhase;
  int _health;
  int _primaryIndex;
  int _secondaryIndex;
  bool _thirdPerson;

  WeaponScript[] _weapons;

  Camera3D _camera;
  Transform3D _fpvLocalTransform;
  Transform3D _mountLocalTransform;
  Node3D _mount;
  CanvasLayer _canvasLayer;
  Label _healthLabel;
  HudScript _hud;
  AudioStreamPlayer3D _bladesAudio;
  AudioStreamPlayer _hitmarkerAudio;
  AudioStreamPlayer _metalImpactAudio;
  AudioStreamPlayer _lockOnAudio;
  AudioStreamGeneratorPlayback _lockOnPlayback;

  public int Health => _health;
  public float CurrentFov => _camera.Fov;
  public float FovMin => fovMin;
  public float FovMax => fovMax;

  public override void _Ready()
  {
    Input.MouseMode = Input.MouseModeEnum.Captured;

    _mount = GetNode<Node3D>("Mount");
    _camera = GetNode<Camera3D>("Mount/Camera3D");
    _fpvLocalTransform = _camera.Transform;
    _mountLocalTransform = _mount.Transform;

    _bladesAudio = GetNode<AudioStreamPlayer3D>("BladesAudio");
    _hitmarkerAudio = GetNode<AudioStreamPlayer>("HitmarkerAudio");
    _metalImpactAudio = GetNode<AudioStreamPlayer>("MetalImpactAudio");

    _bladesAudio.Stream = GD.Load<AudioStream>("res://sounds/49483__lorenzosu__helicopterraw_30sec.wav");
    _hitmarkerAudio.Stream = GD.Load<AudioStream>("res://sounds/hitmarker.mp3");
    _metalImpactAudio.Stream = GD.Load<AudioStream>("res://sounds/620077__ryankingart__metal-impact-trim.mp3");

    _camera.Fov = fovDefault;
    _targetFov = fovDefault;
    _bladesAudio.Finished += () => _bladesAudio.Play();
    _bladesAudio.Play();

    ContactMonitor = true;
    MaxContactsReported = 1;
    BodyEntered += _ => _metalImpactAudio.Play();

    _health = maxHealth;
    _healthLabel = GetNode<Label>("Mount/Camera3D/CanvasLayer/Control/HealthLabel");
    UpdateHealthLabel();

    _canvasLayer = GetNode<CanvasLayer>("Mount/Camera3D/CanvasLayer");
    _hud = new HudScript();
    _canvasLayer.AddChild(_hud);

    var gen = new AudioStreamGenerator { MixRate = 44100f, BufferLength = 0.1f };
    _lockOnAudio = new AudioStreamPlayer { Stream = gen, VolumeDb = -20f };
    AddChild(_lockOnAudio);
    _lockOnAudio.Play();
    _lockOnPlayback = (AudioStreamGeneratorPlayback)_lockOnAudio.GetStreamPlayback();

    // weapons are WeaponScript nodes placed as children of Mount in scene order
    // index 0 = primary (fire), index 1 = secondary (fire_missile)
    _weapons = _mount.GetChildren().OfType<WeaponScript>().ToArray();
    _primaryIndex = 0;
    _secondaryIndex = 1;
  }

  public void PlayHitmarker() => _hitmarkerAudio.Play();

  public void TakeDamage(int amount)
  {
    _metalImpactAudio.Play();
    _health = Mathf.Max(_health - amount, 0);
    UpdateHealthLabel();

    var rng = new RandomNumberGenerator();
    ApplyTorque(new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)).Normalized() * hitSpinForce);

    if (_health <= 0)
    {
      GD.Print("Player dead");
    }
  }

  void UpdateHealthLabel() => _healthLabel.Text = $"HP: {_health}";

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

    if (@event is InputEventKey key && key.Pressed && !key.Echo)
    {
      if (key.Keycode == Key.Escape)
      {
        Input.MouseMode = Input.MouseModeEnum.Visible;
      }
      else if (key.Keycode == Key.R)
      {
        GetTree().ReloadCurrentScene();
      }
      else if (key.Keycode == Key.V)
      {
        _thirdPerson = !_thirdPerson;
        if (!_thirdPerson)
        {
          _mount.Transform = _mountLocalTransform;
          _camera.Transform = _fpvLocalTransform;
        }
      }
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    var throttle = Input.GetAxis("throttle_up", "throttle_down");
    var yaw = Input.GetAxis("yaw_up", "yaw_down");

    ApplyCentralForce(-GlobalTransform.Basis.Y * throttle * throttleForce);

    var fovScale = Mathf.Lerp(0.15f, 1f, (_camera.Fov - fovMin) / (fovMax - fovMin));

    ApplyTorque(GlobalTransform.Basis.Y * yaw * yawTorque * fovScale);

    var rollAxis = -Input.GetAxis("roll_up", "roll_down") * .1f;
    ApplyTorque(-GlobalTransform.Basis.Z * (_rollInput * mouseSensitivity + rollAxis) * rollTorque * fovScale);

    var pitchAxis = Input.GetAxis("pitch_up", "pitch_down") * .1f;
    ApplyTorque(GlobalTransform.Basis.X * (_pitchInput * mouseSensitivity + pitchAxis) * pitchTorque * fovScale);

    _rollInput = 0f;
    _pitchInput = 0f;

    _bladesAudio.PitchScale = Mathf.Lerp(bladePitchIdle, bladePitchMax, Mathf.Abs(throttle));
    _camera.Fov = Mathf.Lerp(_camera.Fov, _targetFov, fovLerpSpeed * (float)delta);

    if (Input.GetActionStrength("fire") > 0.5f && _primaryIndex < _weapons.Length)
    {
      _weapons[_primaryIndex].TryFire();
    }

    if (Input.IsActionPressed("fire_missile") && _secondaryIndex < _weapons.Length)
    {
      _weapons[_secondaryIndex].TryFire();
    }

    _flareTimer -= (float)delta;
    if (Input.IsActionPressed("flare") && _flareTimer <= 0f)
    {
      _flareTimer = flareRate;
      var flare = new FlareScript();
      GetTree().CurrentScene.AddChild(flare);
      flare.Launch(GlobalPosition, LinearVelocity);
    }

    if (_thirdPerson)
    {
      UpdateTpvMount();
    }

    UpdateHud();
    UpdateLockOnAlarm((float)delta);
  }

  void UpdateTpvMount()
  {
    // In TPV, remove the baked FPV tilt from Mount so the drone body looks level.
    // Replace Mount's local basis with an identity basis (no tilt offset).
    _mount.Transform = new Transform3D(Basis.Identity, _mountLocalTransform.Origin);

    // Place the camera 4 units behind and 1.5 units above in Mount-local space
    // (which is now aligned with the Player/RigidBody3D axes).
    _camera.Transform = new Transform3D(Basis.Identity, new Vector3(0, 1.5f, 4f));
  }

  void UpdateLockOnAlarm(float delta)
  {
    var closestDist = float.MaxValue;
    foreach (var node in GetTree().GetNodesInGroup("missiles"))
    {
      if (node is MissileScript m && m.IsHomingOnPlayer(this))
      {
        var dist = GlobalPosition.DistanceTo(m.GlobalPosition);
        if (dist < closestDist)
        {
          closestDist = dist;
        }
      }
    }

    if (closestDist > 9e37f)
    {
      _lockOnBeepTimer = 0f;
      return;
    }

    var beepInterval = Mathf.Lerp(0.08f, 0.6f, Mathf.Clamp(closestDist / 80f, 0f, 1f));
    _lockOnBeepTimer -= delta;
    if (_lockOnBeepTimer <= 0f)
    {
      _lockOnBeepTimer = beepInterval;
      FillBeepTone(880f, 0.06f);
    }
  }

  void FillBeepTone(float frequency, float duration)
  {
    var sampleRate = 44100f;
    var numSamples = (int)(sampleRate * duration);
    var frames = new Vector2[numSamples];
    for (var i = 0; i < numSamples; i++)
    {
      var t = i / sampleRate;
      var envelope = 1f - (i / (float)numSamples);
      var sample = Mathf.Sin(2f * Mathf.Pi * frequency * t + _lockOnPhase) * envelope * 0.6f;
      frames[i] = new Vector2(sample, sample);
    }
    _lockOnPhase = (_lockOnPhase + 2f * Mathf.Pi * frequency * duration) % (2f * Mathf.Pi);
    _lockOnPlayback.PushBuffer(frames);
  }

  void UpdateHud()
  {
    var vel = LinearVelocity;
    _hud.SpeedTotal = vel.Length();
    _hud.SpeedVertical = vel.Y;
    _hud.Altitude = GlobalPosition.Y;

    var forward = -GlobalTransform.Basis.Z;
    _hud.HeadingDeg = (Mathf.RadToDeg(Mathf.Atan2(forward.X, -forward.Z)) + 360f) % 360f;
    _hud.PitchDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(forward.Y, -1f, 1f)));

    var right = GlobalTransform.Basis.X;
    var rightHorizontalLen = new Vector3(right.X, 0, right.Z).Length();
    _hud.BankDeg = Mathf.RadToDeg(Mathf.Atan2(right.Y, rightHorizontalLen));
  }
}
