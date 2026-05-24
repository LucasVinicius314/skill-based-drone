using Godot;

public partial class TargetScript : Node3D
{
  [Export] int maxHealth = 3;

  int _health;
  MeshInstance3D _healthBarFill;

  readonly PackedScene _damageNumberPrefab = GD.Load<PackedScene>("res://prefabs/damage_number.tscn");

  public override void _Ready()
  {
    _health = maxHealth;
    _healthBarFill = GetNode<MeshInstance3D>("HealthBar/Fill");
  }

  public void TakeDamage(int amount, Vector3 hitPosition)
  {
    _health -= amount;

    var t = _healthBarFill.Transform;
    t.Origin = new Vector3(((float)_health / maxHealth - 1f) * 0.5f, 0, 0);
    _healthBarFill.Transform = t;
    _healthBarFill.Scale = new Vector3((float)_health / maxHealth, 1, 1);

    var dmg = _damageNumberPrefab.Instantiate<DamageNumberScript>();
    GetTree().CurrentScene.AddChild(dmg);
    dmg.Show(hitPosition + Vector3.Up * 0.5f, amount);

    if (_health <= 0)
    {
      QueueFree();
    }
  }
}
