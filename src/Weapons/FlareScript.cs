using Godot;

public partial class FlareScript : Node3D
{
  const float Lifetime    = 3.5f;
  const float GlowRadius  = 0.18f;

  static readonly StandardMaterial3D MatFlare = new()
  {
    AlbedoColor       = new Color(1f, 0.6f, 0.1f),
    EmissionEnabled   = true,
    Emission          = new Color(1f, 0.5f, 0.05f),
    EmissionEnergyMultiplier = 6f,
    Transparency      = BaseMaterial3D.TransparencyEnum.Alpha,
  };

  Vector3 _velocity;
  float   _elapsed;
  MeshInstance3D _mesh;

  public void Launch(Vector3 position, Vector3 droneVelocity)
  {
    var rng = new RandomNumberGenerator();
    // drift backward and down, with spread
    _velocity = droneVelocity * 0.3f + new Vector3(
      rng.RandfRange(-3f, 3f),
      rng.RandfRange(-4f, -1f),
      rng.RandfRange(-3f, 3f)
    );

    _mesh = new MeshInstance3D
    {
      Mesh = new SphereMesh { Radius = GlowRadius, Height = GlowRadius * 2f },
      MaterialOverride = (StandardMaterial3D)MatFlare.Duplicate(),
    };
    AddChild(_mesh);
    GlobalPosition = position;
  }

  public override void _Ready()
  {
    AddToGroup("flares");
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    if (_elapsed >= Lifetime)
    {
      QueueFree();
      return;
    }

    GlobalPosition += _velocity * (float)delta;
    _velocity.Y -= 4f * (float)delta; // gravity

    // fade out in the last second
    var alpha = Mathf.Clamp(1f - ((_elapsed - (Lifetime - 1f)) / 1f), 0f, 1f);
    var mat = (StandardMaterial3D)_mesh.MaterialOverride;
    mat.AlbedoColor = new Color(1f, 0.6f, 0.1f, alpha);
    mat.Emission    = new Color(1f, 0.5f, 0.05f) * alpha;
  }
}
