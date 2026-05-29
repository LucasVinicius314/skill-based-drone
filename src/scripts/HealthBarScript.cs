using Godot;

public partial class HealthBarScript : Node3D
{
  [Export] public float width = 7.5f;
  [Export] public float height = 0.6f;

  MeshInstance3D _fill;
  MeshInstance3D _ghost;
  StandardMaterial3D _ghostMat;
  float _ghostRatio;
  float _ghostAlpha;
  Camera3D _camera;

  const float GhostFadeDelay = 0.5f;
  const float GhostFadeSpeed = 1.5f;
  float _ghostFadeTimer;

  int _current;
  int _max;

  public override void _Ready()
  {
    var player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");
    _camera = player.GetNode<Camera3D>("Mount/Camera3D");

    var bgMat = new StandardMaterial3D
    {
      AlbedoColor = new Color(0.15f, 0.15f, 0.15f),
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      NoDepthTest = true,
      RenderPriority = 1,
    };
    AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(width, height), Material = bgMat } });

    _ghostMat = new StandardMaterial3D
    {
      AlbedoColor = new Color(1f, 0.85f, 0.3f, 0f),
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      NoDepthTest = true,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      RenderPriority = 2,
    };
    _ghost = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0f, height), Material = _ghostMat } };
    AddChild(_ghost);

    var fillMat = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      NoDepthTest = true,
      RenderPriority = 3,
    };
    _fill = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(width, height), Material = fillMat } };
    AddChild(_fill);
  }

  public override void _Process(double delta)
  {
    if (_camera != null && GlobalPosition.DistanceTo(_camera.GlobalPosition) > 0.1f)
    {
      var dir = (_camera.GlobalPosition - GlobalPosition).Normalized();
      if (dir.Cross(Vector3.Up).LengthSquared() > 0.001f)
      {
        LookAt(_camera.GlobalPosition, Vector3.Up);
        RotateObjectLocal(Vector3.Up, Mathf.Pi);
      }
    }

    if (_ghostAlpha <= 0f) return;

    _ghostFadeTimer -= (float)delta;
    if (_ghostFadeTimer <= 0f)
    {
      _ghostAlpha = Mathf.Max(0f, _ghostAlpha - GhostFadeSpeed * (float)delta);
      _ghostMat.AlbedoColor = new Color(1f, 0.85f, 0.3f, _ghostAlpha);
    }
  }

  public void Init(int max)
  {
    _max = max;
    _current = max;
    _ghostRatio = 1f;
    _ghostAlpha = 0f;
    Refresh();
  }

  public void SetRatio(float ratio)
  {
    _ghostRatio = _max > 0 ? (float)_current / _max : 0f;
    _ghostAlpha = 1f;
    _ghostFadeTimer = GhostFadeDelay;
    _current = Mathf.RoundToInt(ratio * _max);
    Refresh();
  }

  void Refresh()
  {
    if (_fill == null) return;
    var ratio = _max > 0 ? (float)_current / _max : 0f;

    var fillWidth = width * ratio;
    ((QuadMesh)_fill.Mesh).Size = new Vector2(fillWidth, height);
    _fill.Position = new Vector3((fillWidth - width) * 0.5f, 0, 0);
    ((StandardMaterial3D)((QuadMesh)_fill.Mesh).Material).AlbedoColor =
      Color.FromHsv(Mathf.Lerp(0f, 0.33f, ratio), 1f, 0.9f);

    var ghostWidth = width * Mathf.Max(0f, _ghostRatio - ratio);
    ((QuadMesh)_ghost.Mesh).Size = new Vector2(ghostWidth, height);
    var fillRightEdge = -width * 0.5f + fillWidth;
    _ghost.Position = new Vector3(fillRightEdge + ghostWidth * 0.5f, 0, 0);
    _ghostMat.AlbedoColor = new Color(1f, 0.85f, 0.3f, _ghostAlpha);
  }
}
