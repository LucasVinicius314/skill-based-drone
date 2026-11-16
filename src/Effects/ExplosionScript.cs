using Godot;

public partial class ExplosionScript : Node3D
{
  [Export] float duration = 5.6f;

  float _elapsed;
  float _radius = 1.2f;
  MeshInstance3D _flash;
  MeshInstance3D _shockwave;
  GpuParticles3D _smoke;

  public void Init(float radius)
  {
    _radius = radius * 0.6f;
  }

  public override void _Ready()
  {
    SpawnFlash();
    SpawnShockwave();
    SpawnFireball();
    SpawnDebris();
    SpawnSparks();
    SpawnSmoke();
  }

  void SpawnFlash()
  {
    _flash = new MeshInstance3D
    {
      Mesh = new SphereMesh { Radius = _radius * 0.6f, Height = _radius * 1.2f },
      MaterialOverride = new StandardMaterial3D
      {
        AlbedoColor = new Color(1f, 1f, 0.8f),
        EmissionEnabled = true,
        Emission = new Color(1f, 0.9f, 0.5f),
        EmissionEnergyMultiplier = 8f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      },
    };
    AddChild(_flash);
  }

  void SpawnShockwave()
  {
    _shockwave = new MeshInstance3D
    {
      Mesh = new TorusMesh { InnerRadius = 0f, OuterRadius = _radius * 0.3f, Rings = 8, RingSegments = 32 },
      MaterialOverride = new StandardMaterial3D
      {
        AlbedoColor = new Color(1f, 0.7f, 0.3f, 0.6f),
        EmissionEnabled = true,
        Emission = new Color(1f, 0.5f, 0.1f),
        EmissionEnergyMultiplier = 3f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      },
    };
    AddChild(_shockwave);
  }

  void SpawnFireball()
  {
    var fireball = new GpuParticles3D
    {
      Amount = 60,
      Lifetime = duration * 0.7f,
      OneShot = true,
      Explosiveness = 0.9f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
        EmissionSphereRadius = _radius * 0.2f,
        Direction = Vector3.Up,
        Spread = 180f,
        InitialVelocityMin = _radius * 1f,
        InitialVelocityMax = _radius * 3f,
        Gravity = new Vector3(0, _radius * -0.5f, 0),
        ScaleMin = _radius * 0.15f,
        ScaleMax = _radius * 0.4f,
        Color = new Color(1f, 0.45f, 0.05f),
        DampingMin = 6f,
        DampingMax = 6f,
      },
      DrawPass1 = new SphereMesh { Radius = 1f, Height = 2f },
    };
    AddChild(fireball);
    fireball.Restart();
  }

  void SpawnDebris()
  {
    var debris = new GpuParticles3D
    {
      Amount = 24,
      Lifetime = duration * 1.2f,
      OneShot = true,
      Explosiveness = 1f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
        EmissionSphereRadius = _radius * 0.1f,
        Direction = Vector3.Up,
        Spread = 180f,
        InitialVelocityMin = _radius * 2f,
        InitialVelocityMax = _radius * 5f,
        Gravity = new Vector3(0, -18f, 0),
        ScaleMin = _radius * 0.04f,
        ScaleMax = _radius * 0.1f,
        Color = new Color(0.2f, 0.2f, 0.2f),
        DampingMin = 2f,
        DampingMax = 2f,
      },
      DrawPass1 = new BoxMesh { Size = Vector3.One },
    };
    AddChild(debris);
    debris.Restart();
  }

  void SpawnSparks()
  {
    var sparks = new GpuParticles3D
    {
      Amount = 80,
      Lifetime = duration * 0.6f,
      OneShot = true,
      Explosiveness = 1f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
        EmissionSphereRadius = _radius * 0.1f,
        Direction = Vector3.Up,
        Spread = 180f,
        InitialVelocityMin = _radius * 3f,
        InitialVelocityMax = _radius * 8f,
        Gravity = new Vector3(0, -12f, 0),
        ScaleMin = _radius * 0.015f,
        ScaleMax = _radius * 0.03f,
        Color = new Color(1f, 0.9f, 0.2f),
        DampingMin = 1f,
        DampingMax = 1f,
      },
      DrawPass1 = new SphereMesh { Radius = 1f, Height = 2f },
    };
    AddChild(sparks);
    sparks.Restart();
  }

  void SpawnSmoke()
  {
    _smoke = new GpuParticles3D
    {
      Amount = 30,
      Lifetime = 8f,
      OneShot = true,
      Explosiveness = 0.6f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
        EmissionSphereRadius = _radius * 0.3f,
        Direction = Vector3.Up,
        Spread = 60f,
        InitialVelocityMin = _radius * 0.5f,
        InitialVelocityMax = _radius * 1.5f,
        Gravity = new Vector3(0, _radius * 0.3f, 0),
        ScaleMin = _radius * 0.4f,
        ScaleMax = _radius * 1.2f,
        DampingMin = 1f,
        DampingMax = 1f,
        ColorRamp = MakeSmokeGradient(),
        ScaleCurve = MakeScaleCurve(),
      },
      DrawPass1 = new SphereMesh { Radius = 1f, Height = 2f },
    };
    AddChild(_smoke);
    _smoke.Restart();

    if (GetNodeOrNull<AudioStreamPlayer3D>("Audio") is { } audio)
    {
      audio.Finished += audio.QueueFree;
    }
  }

  static GradientTexture1D MakeSmokeGradient()
  {
    var gradient = new Gradient();
    gradient.SetColor(0, new Color(0.5f, 0.5f, 0.5f, 0.4f));
    gradient.AddPoint(0.4f, new Color(0.35f, 0.35f, 0.35f, 0.25f));
    gradient.AddPoint(0.75f, new Color(0.25f, 0.25f, 0.25f, 0.1f));
    gradient.SetColor(1, new Color(0.2f, 0.2f, 0.2f, 0f));
    return new GradientTexture1D { Gradient = gradient };
  }

  static CurveTexture MakeScaleCurve()
  {
    var curve = new Curve();
    curve.AddPoint(new Vector2(0f, 0.3f));
    curve.AddPoint(new Vector2(0.5f, 1f));
    curve.AddPoint(new Vector2(1f, 0f));
    return new CurveTexture { Curve = curve };
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    var t = _elapsed / duration;

    if (_flash != null)
    {
      var scale = Mathf.Lerp(1f, 3f, Mathf.Pow(t, 0.3f));
      _flash.Scale = Vector3.One * scale;
      (_flash.MaterialOverride as StandardMaterial3D).AlbedoColor = new Color(1f, Mathf.Lerp(1f, 0.3f, t), 0.1f, Mathf.Pow(1f - t, 2f));
    }

    if (_shockwave != null)
    {
      var scale = Mathf.Lerp(0.5f, _radius * 1.5f, Mathf.Pow(t, 0.4f));
      _shockwave.Scale = new Vector3(scale, 1f, scale);
      (_shockwave.MaterialOverride as StandardMaterial3D).AlbedoColor = new Color(1f, 0.6f, 0.2f, Mathf.Pow(1f - t, 3f));
    }

    if (_elapsed >= duration)
    {
      if (IsInstanceValid(_smoke))
      {
        var smokePos = _smoke.GlobalPosition;
        RemoveChild(_smoke);
        GetTree().CurrentScene.AddChild(_smoke);
        _smoke.GlobalPosition = smokePos;
        _smoke.Finished += _smoke.QueueFree;
      }
      QueueFree();
    }
  }
}
