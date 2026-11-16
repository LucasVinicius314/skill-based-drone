using Godot;
using System.Collections.Generic;
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

  // replicated from the owner to every other peer, which shows this drone as a frozen puppet that follows them
  [Export] public Vector3 SyncPosition { get; set; }
  [Export] public Quaternion SyncRotation { get; set; } = Quaternion.Identity;
  [Export] public Vector3 SyncVelocity { get; set; }

  static readonly List<PlayerScript> _all = new();

  // the drone this peer flies (null before it exists)
  public static PlayerScript Local { get; private set; }

  public static PlayerScript ByPeer(int peer)
  {
    foreach (var player in _all)
    {
      if (player.PeerId == peer)
      {
        return player;
      }
    }
    return null;
  }

  public static PlayerScript Nearest(Vector3 from)
  {
    PlayerScript best = null;
    var bestDistance = float.MaxValue;
    foreach (var player in _all)
    {
      var distance = player.GlobalPosition.DistanceSquaredTo(from);
      if (distance < bestDistance)
      {
        bestDistance = distance;
        best = player;
      }
    }
    return best;
  }

  float globalRotationScale = 12f;

  bool _placed;
  Label3D _nameTag;
  float _targetFov;
  float _pitchInput;
  float _rollInput;
  float _flareTimer;
  float _lockOnBeepTimer;
  float _lockOnPhase;
  float _shakeTrauma;
  float _tpvZOffset;
  float _orbitYaw;
  float _orbitPitch;
  float _fovScale = 1f;
  int _health;
  int _primaryIndex;
  int _secondaryIndex = 1;
  bool _thirdPerson;
  bool _freeLook;

  WeaponScript[] _weapons;

  Camera3D _camera;
  Transform3D _fpvLocalTransform;
  Transform3D _mountLocalTransform;
  Node3D _mount;
  Node3D _cameraRig;
  CanvasLayer _canvasLayer;
  Label _healthLabel;
  HudScript _hud;
  PauseMenuScript _pauseMenu;
  AudioStreamPlayer3D _bladesAudio;
  AudioStreamPlayer _hitmarkerAudio;
  AudioStreamPlayer _metalImpactAudio;
  AudioStreamPlayer _lockOnAudio;
  AudioStreamGeneratorPlayback _lockOnPlayback;

  // node name = owning peer id (set by the spawner), so every peer can find any player by it
  public int PeerId => int.TryParse(Name, out var id) ? id : 1;
  public bool IsLocal { get; private set; }
  public Camera3D Camera => _camera;
  public CanvasLayer HudLayer => _canvasLayer;
  public WeaponScript Weapon(int index) => _weapons != null && index >= 0 && index < _weapons.Length ? _weapons[index] : null;
  public int Health => _health;
  public float CurrentFov => _camera.Fov;
  public float FovMin => fovMin;
  public float FovMax => fovMax;

  public override void _EnterTree()
  {
    // players spawned by the network are named after their peer id, which is also who owns them
    if (int.TryParse(Name, out var peer))
    {
      SetMultiplayerAuthority(peer);
    }
    _all.Add(this);
  }

  public override void _ExitTree()
  {
    _all.Remove(this);
    if (_nameTag != null)
    {
      NetSession.Instance.RosterChanged -= UpdateNameTag;
    }
    if (Local == this)
    {
      Local = null;
    }
  }

  public override void _Ready()
  {
    IsLocal = IsMultiplayerAuthority();

    _mount = GetNode<Node3D>("Mount");
    _cameraRig = GetNode<Node3D>("CameraRig");
    _camera = GetNode<Camera3D>("Mount/Camera3D");
    _bladesAudio = GetNode<AudioStreamPlayer3D>("BladesAudio");
    _bladesAudio.Stream = GD.Load<AudioStream>("res://sounds/49483__lorenzosu__helicopterraw_30sec.wav");
    _bladesAudio.Finished += () => _bladesAudio.Play();
    _bladesAudio.Play();

    // weapons are WeaponScript nodes placed as children of Mount in scene order
    // index 0 = primary (fire), index 1 = secondary (fire_missile)
    _weapons = _mount.GetChildren().OfType<WeaponScript>().ToArray();
    for (var i = 0; i < _weapons.Length; i++)
    {
      _weapons[i].Index = i;
    }

    if (!IsLocal)
    {
      SetupPuppet();
      return;
    }

    Local = this;
    Input.MouseMode = Input.MouseModeEnum.Captured;
    _camera.MakeCurrent();
    _fpvLocalTransform = _camera.Transform;
    _mountLocalTransform = _mount.Transform;

    _hitmarkerAudio = GetNode<AudioStreamPlayer>("HitmarkerAudio");
    _metalImpactAudio = GetNode<AudioStreamPlayer>("MetalImpactAudio");
    _hitmarkerAudio.Stream = GD.Load<AudioStream>("res://sounds/hitmarker.mp3");
    _metalImpactAudio.Stream = GD.Load<AudioStream>("res://sounds/620077__ryankingart__metal-impact-trim.mp3");

    _camera.Fov = fovDefault;
    _targetFov = fovDefault;

    ContactMonitor = true;
    MaxContactsReported = 1;
    BodyEntered += _ =>
    {
      _metalImpactAudio.Play();
      AddShake(0.5f);
    };

    _health = maxHealth;
    _healthLabel = GetNode<Label>("Mount/Camera3D/CanvasLayer/Control/HealthLabel");
    UpdateHealthLabel();

    _canvasLayer = GetNode<CanvasLayer>("Mount/Camera3D/CanvasLayer");
    _hud = new HudScript();
    _canvasLayer.AddChild(_hud);
    _pauseMenu = new PauseMenuScript();
    _canvasLayer.AddChild(_pauseMenu);

    var gen = new AudioStreamGenerator { MixRate = 44100f, BufferLength = 0.1f };
    _lockOnAudio = new AudioStreamPlayer { Stream = gen, VolumeDb = -20f };
    AddChild(_lockOnAudio);
    _lockOnAudio.Play();
    _lockOnPlayback = (AudioStreamGeneratorPlayback)_lockOnAudio.GetStreamPlayback();

    _hud.Weapons = _weapons;
    _hud.Camera = _camera;
    _hud.BulletDropPoints = new Vector3?[_weapons.Length];
  }

  // another player drone: no camera, HUD or input, and it does not simulate. it follows what its owner replicates
  void SetupPuppet()
  {
    _camera.Free(); // the HUD canvas layer hangs under it
    _camera = null;
    Freeze = true;
    FreezeMode = FreezeModeEnum.Kinematic;

    _nameTag = new Label3D
    {
      Text = NetSession.Instance.NameOf(PeerId),
      Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
      FixedSize = true,
      NoDepthTest = true,
      PixelSize = 0.0012f,
      FontSize = 32,
      Modulate = new Color(0f, 1f, 0.4f, 0.9f),
      Position = new Vector3(0f, 2.2f, 0f),
    };
    AddChild(_nameTag);

    // the roster usually arrives after the drone does
    NetSession.Instance.RosterChanged += UpdateNameTag;
  }

  void UpdateNameTag() => _nameTag.Text = NetSession.Instance.NameOf(PeerId);

  public void PlayHitmarker() => _hitmarkerAudio.Play();

  public void TakeDamage(int amount)
  {
    _metalImpactAudio.Play();
    _health = Mathf.Max(_health - amount, 0);
    UpdateHealthLabel();
    AddShake(0.7f);

    var rng = new RandomNumberGenerator();
    ApplyTorque(new Vector3(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f)).Normalized() * hitSpinForce);

    if (_health <= 0)
    {
      GD.Print("Player dead");
    }
  }

  public void AddShake(float trauma) => _shakeTrauma = Mathf.Min(_shakeTrauma + trauma, 1f);

  void UpdateHealthLabel() => _healthLabel.Text = $"HP: {_health}";

  public override void _Input(InputEvent @event)
  {
    if (!IsLocal)
    {
      return;
    }

    if (@event is InputEventMouseMotion motion && !_pauseMenu.IsOpen)
    {
      if (_freeLook)
      {
        _orbitYaw -= motion.Relative.X * mouseSensitivity * 0.25f * _fovScale;
        _orbitPitch -= motion.Relative.Y * mouseSensitivity * 0.25f * _fovScale;
        _orbitPitch = Mathf.Clamp(_orbitPitch, -Mathf.Pi * 0.45f, Mathf.Pi * 0.45f);
      }
      else
      {
        _rollInput = motion.Relative.X;
        _pitchInput = motion.Relative.Y;
      }
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
      HandleKeyInput(key.Keycode);
    }
  }

  void HandleKeyInput(Key keycode)
  {
    if (keycode == Key.Escape && !GetTree().Paused)
    {
      _pauseMenu.Toggle();
    }
    else if (keycode == Key.R && !NetSession.Instance.IsOnline)
    {
      GetTree().ReloadCurrentScene();
    }
    else if (keycode == Key.V)
    {
      _thirdPerson = !_thirdPerson;
      _orbitYaw = 0f;
      _orbitPitch = 0f;
      _freeLook = false;
      if (_thirdPerson)
      {
        _mount.RemoveChild(_camera);
        _cameraRig.AddChild(_camera);
      }
      else
      {
        _cameraRig.RemoveChild(_camera);
        _mount.AddChild(_camera);
        _mount.Transform = _mountLocalTransform;
        _camera.Transform = _fpvLocalTransform;
      }
    }
    else if (keycode == Key.Alt)
    {
      _freeLook = !_freeLook;
      _orbitYaw = 0f;
      _orbitPitch = 0f;
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (!IsLocal)
    {
      return;
    }

    // while the pause menu is open the drone keeps drifting (online the world cannot stop) but ignores the controls
    var controls = !_pauseMenu.IsOpen;
    var throttle = controls ? Input.GetAxis("throttle_up", "throttle_down") : 0f;
    var yaw = controls ? Input.GetAxis("yaw_up", "yaw_down") : 0f;

    ApplyCentralForce(-GlobalTransform.Basis.Y * throttle * throttleForce);

    _fovScale = Mathf.Lerp(0.15f, 1f, (_camera.Fov - fovMin) / (fovMax - fovMin));
    var fovScale = _fovScale;

    ApplyTorque(GlobalTransform.Basis.Y * yaw * yawTorque * fovScale * globalRotationScale);

    var rollAxis = controls ? -Input.GetAxis("roll_up", "roll_down") * .1f : 0f;
    ApplyTorque(-GlobalTransform.Basis.Z * (_rollInput * mouseSensitivity + rollAxis) * rollTorque * fovScale * globalRotationScale);

    var pitchAxis = controls ? Input.GetAxis("pitch_up", "pitch_down") * .1f : 0f;
    ApplyTorque(GlobalTransform.Basis.X * (_pitchInput * mouseSensitivity + pitchAxis) * pitchTorque * fovScale * globalRotationScale);

    _rollInput = 0f;
    _pitchInput = 0f;

    _bladesAudio.PitchScale = Mathf.Lerp(bladePitchIdle, bladePitchMax, Mathf.Abs(throttle));
    _camera.Fov = Mathf.Lerp(_camera.Fov, _targetFov, fovLerpSpeed * (float)delta);

    if (controls && Input.GetActionStrength("fire") > 0.5f && _primaryIndex < _weapons.Length && _weapons[_primaryIndex].TryFire())
    {
      AddShake(0.04f);
    }

    if (controls && Input.IsActionPressed("fire_missile") && _secondaryIndex < _weapons.Length && _weapons[_secondaryIndex].TryFire())
    {
      AddShake(0.15f);
    }

    _flareTimer -= (float)delta;
    if (controls && Input.IsActionPressed("flare") && _flareTimer <= 0f)
    {
      _flareTimer = flareRate;
      Projectiles.SpawnFlare(this, GlobalPosition, LinearVelocity);
      NetSession.Instance.AnnounceFlare(GlobalPosition, LinearVelocity);
    }

    if (_thirdPerson)
    {
      UpdateTpvCamera((float)delta);
    }

    UpdateShake((float)delta);
    UpdateHud();
    UpdateLockOnAlarm((float)delta);

    SyncPosition = GlobalPosition;
    SyncRotation = GlobalTransform.Basis.GetRotationQuaternion();
    SyncVelocity = LinearVelocity;
  }

  public override void _Process(double delta)
  {
    if (!IsLocal)
    {
      FollowOwner((float)delta);
    }
  }

  // puppets only: ease toward the owner latest pose
  void FollowOwner(float delta)
  {
    if (SyncPosition == Vector3.Zero)
    {
      return;
    }

    if (!_placed || GlobalPosition.DistanceSquaredTo(SyncPosition) > 2500f)
    {
      _placed = true;
      GlobalTransform = new Transform3D(new Basis(SyncRotation), SyncPosition);
    }
    else
    {
      var follow = 1f - Mathf.Exp(-20f * delta);
      var basis = new Basis(GlobalTransform.Basis.GetRotationQuaternion().Slerp(SyncRotation, follow));
      GlobalTransform = new Transform3D(basis, GlobalPosition.Lerp(SyncPosition, follow));
    }

    _bladesAudio.PitchScale = Mathf.Lerp(bladePitchIdle, bladePitchMax, Mathf.Clamp(SyncVelocity.Length() / 30f, 0f, 1f));
  }

  // TPV: CameraRig sits at player root. Camera local position = behind/above offset.
  // Locked: rig has identity rotation, camera follows the drone with velocity lean.
  // Free look: rig is rotated by _orbitYaw/_orbitPitch — camera orbits the player center naturally.
  void UpdateTpvCamera(float delta)
  {
    var localVel = ToLocal(GlobalPosition + LinearVelocity) - ToLocal(GlobalPosition);
    var targetZ = Mathf.Lerp(10f, 16f, Mathf.Clamp(LinearVelocity.Length() / 40f, 0f, 1f));
    _tpvZOffset = Mathf.Lerp(_tpvZOffset, targetZ, 4f * delta);

    if (!_freeLook)
    {
      var lateralLean = Mathf.Clamp(localVel.X * 0.08f, -1.2f, 1.2f);
      var verticalLean = Mathf.Clamp(-localVel.Y * 0.05f, -0.8f, 0.8f);
      _cameraRig.Basis = Basis.Identity;
      _mount.Transform = _mountLocalTransform;
      _camera.Transform = new Transform3D(Basis.Identity, new Vector3(lateralLean, 3f + verticalLean, _tpvZOffset));
    }
    else
    {
      var yawRot = new Basis(Vector3.Up, _orbitYaw);
      var pitchRot = new Basis(yawRot * Vector3.Right, _orbitPitch);
      _cameraRig.Basis = pitchRot * yawRot;
      _camera.Transform = new Transform3D(Basis.Identity, new Vector3(0f, 3f, _tpvZOffset));

      AimMountAtCameraTarget();
    }
  }

  void AimMountAtCameraTarget()
  {
    var camOrigin = _camera.GlobalPosition;
    var camForward = -_camera.GlobalTransform.Basis.Z;
    const float rayLength = 2000f;

    var query = PhysicsRayQueryParameters3D.Create(camOrigin, camOrigin + camForward * rayLength);
    query.CollideWithAreas = false;
    query.CollideWithBodies = true;
    query.Exclude = [GetRid()];

    var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    var aimPoint = result.Count > 0
      ? result["position"].AsVector3()
      : camOrigin + camForward * rayLength;

    var dir = aimPoint - _mount.GlobalPosition;
    if (dir.LengthSquared() < 0.001f)
    {
      return;
    }
    var up = GlobalTransform.Basis.Y;
    if (dir.Normalized().Cross(up).LengthSquared() < 0.001f)
    {
      return;
    }
    _mount.LookAt(aimPoint, up);
  }

  void UpdateShake(float delta)
  {
    _shakeTrauma = Mathf.Max(_shakeTrauma - delta * 1.8f, 0f);
    var shake = _shakeTrauma * _shakeTrauma;
    if (shake < 0.001f)
    {
      // first person: snap back to the resting spot, or the last shake offset would stay
      if (!_thirdPerson)
      {
        _camera.Transform = _fpvLocalTransform;
      }
      return;
    }

    var n = (float)GD.RandRange(0.0, 1.0);
    var offsetX = Mathf.Lerp(-0.06f, 0.06f, n) * shake;
    var offsetY = Mathf.Lerp(-0.06f, 0.06f, (float)GD.RandRange(0.0, 1.0)) * shake;
    var shakeOffset = new Vector3(offsetX, offsetY, 0f);

    // first person restarts from the resting transform every frame (adding to the current one made the offsets pile up);
    // third person is rewritten every frame by UpdateTpvCamera, so adding to it is safe
    var baseTransform = _thirdPerson ? _camera.Transform : _fpvLocalTransform;
    _camera.Transform = new Transform3D(baseTransform.Basis, baseTransform.Origin + shakeOffset);
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

  void UpdateBulletDropPredictions()
  {
    if (_hud.BulletDropPoints == null) return;
    for (var i = 0; i < _weapons.Length; i++)
    {
      _hud.BulletDropPoints[i] = _weapons[i].PredictImpact(GetWorld3D(), GetRid());
    }
  }

  void UpdateHud()
  {
    UpdateBulletDropPredictions();
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
