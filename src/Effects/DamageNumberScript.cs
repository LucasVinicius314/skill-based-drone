using Godot;

public partial class DamageNumberScript : Node3D
{
  [Export] float riseSpeed = 2f;
  [Export] float lifetime = 1f;

  float _elapsed;
  Label _label;
  Camera3D _camera;

  public void Show(Vector3 position, int damage, Camera3D camera, CanvasLayer canvasLayer)
  {
    GlobalPosition = position;
    _camera = camera;

    _label = new Label { Text = damage.ToString() };
    _label.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f));
    _label.AddThemeFontSizeOverride("font_size", 18);
    _label.AddThemeColorOverride("font_outline_color", Colors.Black);
    _label.AddThemeConstantOverride("outline_size", 4);
    canvasLayer.AddChild(_label);
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    GlobalPosition += Vector3.Up * riseSpeed * (float)delta;

    if (_camera != null && _label != null)
    {
      var screenPos = _camera.UnprojectPosition(GlobalPosition);
      _label.Position = screenPos - _label.Size / 2f;
      _label.Modulate = new Color(1, 1, 1, 1f - _elapsed / lifetime);
    }

    if (_elapsed >= lifetime)
    {
      _label?.QueueFree();
      QueueFree();
    }
  }

  public override void _ExitTree()
  {
    if (IsInstanceValid(_label)) _label.QueueFree();
  }
}
