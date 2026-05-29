using Godot;

public partial class WormSegmentScript : EnemyScript
{
  public WormScript Worm;

  protected override void OnReady()
  {
    maxHealth = 2;
    explosionRadius = 2f;
    AddToGroup("worm_segments");
  }

  public override void TakeDamage(int amount, Vector3 hitPosition)
  {
    if (_health - amount <= 0)
    {
      Worm?.OnSegmentDied(this);
    }
    base.TakeDamage(amount, hitPosition);
  }

  public void SetColor(Color color)
  {
    var mat = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = mat;
  }
}
