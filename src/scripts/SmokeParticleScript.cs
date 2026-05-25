using Godot;

public partial class SmokeParticleScript : MeshInstance3D
{
  float _lifetime;
  float _elapsed;
  float _startSize;
  float _endSize;
  StandardMaterial3D _mat;

  public void Init(float lifetime, float startSize, float endSize)
  {
    _lifetime = lifetime;
    _startSize = startSize;
    _endSize = endSize;

    _mat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
      AlbedoColor = new Color(0.75f, 0.75f, 0.75f, 0.35f),
    };

    Mesh = new QuadMesh { Size = Vector2.One * startSize };
    MaterialOverride = _mat;
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    var t = _elapsed / _lifetime;

    var size = Mathf.Lerp(_startSize, _endSize, t);
    Scale = Vector3.One * size / _startSize;
    _mat.AlbedoColor = new Color(0.75f, 0.75f, 0.75f, Mathf.Lerp(0.35f, 0f, t));

    if (_elapsed >= _lifetime)
    {
      QueueFree();
    }
  }
}
