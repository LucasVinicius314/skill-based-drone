using Godot;

public partial class DamageNumberScript : Label3D
{
  [Export] float riseSpeed = 2f;
  [Export] float lifetime = 1f;

  float _elapsed;

  public void Show(Vector3 position, int damage)
  {
    GlobalPosition = position;
    Text = damage.ToString();
  }

  public override void _Process(double delta)
  {
    _elapsed += (float)delta;
    GlobalPosition += Vector3.Up * riseSpeed * (float)delta;
    Modulate = new Color(1, 1, 0, 1f - _elapsed / lifetime);
    if (_elapsed >= lifetime)
    {
      QueueFree();
    }
  }
}
