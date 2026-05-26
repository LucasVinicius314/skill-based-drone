using Godot;

public partial class SmokeParticleScript : MeshInstance3D
{
  float _lifetime;
  float _elapsed;
  float _startSize;
  float _endSize;
  Vector3 _drift;
  StandardMaterial3D _mat;

  static readonly SphereMesh SharedMesh = new() { Radius = 0.5f, Height = 1f, RadialSegments = 6, Rings = 3 };

  public void Init(float lifetime, float startSize, float endSize, Vector3 drift = default)
  {
    _lifetime = lifetime;
    _startSize = startSize;
    _endSize = endSize;
    _drift = drift;

    _mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      AlbedoColor = new Color(0.75f, 0.75f, 0.75f, 0.35f),
    };

    Mesh = SharedMesh;
    MaterialOverride = _mat;
    Scale = Vector3.One * startSize;
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    var t = _elapsed / _lifetime;

    var size = Mathf.Lerp(_startSize, _endSize, t);
    Scale = Vector3.One * size / _startSize;
    _mat.AlbedoColor = new Color(0.75f, 0.75f, 0.75f, Mathf.Lerp(0.35f, 0f, t));
    Position += _drift * (float)delta;

    if (_elapsed >= _lifetime)
    {
      QueueFree();
    }
  }
}
