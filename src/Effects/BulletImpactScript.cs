using Godot;

public partial class BulletImpactScript : Node3D
{
  public override void _Ready()
  {
    SpawnSparks();
    SpawnDust();
  }

  void SpawnSparks()
  {
    var sparks = new GpuParticles3D
    {
      Amount = 12,
      Lifetime = 0.4f,
      OneShot = true,
      Explosiveness = 1f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point,
        Direction = Vector3.Up,
        Spread = 180f,
        InitialVelocityMin = 4f,
        InitialVelocityMax = 12f,
        Gravity = new Vector3(0, -18f, 0),
        ScaleMin = 0.02f,
        ScaleMax = 0.05f,
        Color = new Color(1f, 0.85f, 0.3f),
        DampingMin = 8f,
        DampingMax = 12f,
      },
      DrawPass1 = new SphereMesh { Radius = 1f, Height = 2f },
    };
    AddChild(sparks);
    sparks.Finished += sparks.QueueFree;
    sparks.Restart();
  }

  void SpawnDust()
  {
    var dust = new GpuParticles3D
    {
      Amount = 8,
      Lifetime = 0.6f,
      OneShot = true,
      Explosiveness = 0.9f,
      ProcessMaterial = new ParticleProcessMaterial
      {
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point,
        Direction = Vector3.Up,
        Spread = 90f,
        InitialVelocityMin = 1f,
        InitialVelocityMax = 3f,
        Gravity = new Vector3(0, 1f, 0),
        ScaleMin = 0.08f,
        ScaleMax = 0.2f,
        DampingMin = 4f,
        DampingMax = 6f,
        ColorRamp = MakeDustGradient(),
      },
      DrawPass1 = new SphereMesh { Radius = 1f, Height = 2f },
    };
    AddChild(dust);
    dust.Finished += QueueFree;
    dust.Restart();
  }

  static GradientTexture1D MakeDustGradient()
  {
    var gradient = new Gradient();
    gradient.SetColor(0, new Color(0.7f, 0.6f, 0.45f, 0.6f));
    gradient.SetColor(1, new Color(0.5f, 0.45f, 0.35f, 0f));
    return new GradientTexture1D { Gradient = gradient };
  }
}
