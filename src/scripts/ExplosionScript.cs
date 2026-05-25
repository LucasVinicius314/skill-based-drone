using Godot;

public partial class ExplosionScript : Node3D
{
  [Export] float duration = 0.6f;

  float _elapsed;
  float _radius = 1.2f;
  MeshInstance3D _flash;

  public void Init(float radius)
  {
    _radius = radius;
  }

  public override void _Ready()
  {
    SpawnFlash();
    SpawnParticles();
  }

  void SpawnFlash()
  {
    _flash = new MeshInstance3D
    {
      Mesh = new SphereMesh { Radius = _radius, Height = _radius * 2f },
      MaterialOverride = new StandardMaterial3D
      {
        AlbedoColor = new Color(1f, 0.6f, 0.1f),
        EmissionEnabled = true,
        Emission = new Color(1f, 0.4f, 0f),
        EmissionEnergyMultiplier = 4f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      },
    };
    AddChild(_flash);
  }

  void SpawnParticles()
  {
    var particles = new GpuParticles3D
    {
      Amount = 48,
      Lifetime = duration,
      OneShot = true,
      Explosiveness = 1f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
        EmissionSphereRadius = _radius * 0.3f,
        Direction = Vector3.Up,
        Spread = 180f,
        InitialVelocityMin = _radius * 0.8f,
        InitialVelocityMax = _radius * 2.4f,
        Gravity = new Vector3(0, -6f, 0),
        ScaleMin = 0.08f,
        ScaleMax = 0.22f,
        Color = new Color(1f, 0.5f, 0.1f),
      },
      DrawPass1 = new SphereMesh { Radius = 0.08f, Height = 0.16f },
    };
    AddChild(particles);
    particles.Restart();
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    var t = _elapsed / duration;

    if (_flash != null)
    {
      var scale = Mathf.Lerp(1f, 2.5f, t);
      _flash.Scale = Vector3.One * scale;
      (_flash.MaterialOverride as StandardMaterial3D).AlbedoColor = new Color(1f, 0.5f, 0.1f, 1f - t);
    }

    if (_elapsed >= duration)
    {
      QueueFree();
    }
  }
}
