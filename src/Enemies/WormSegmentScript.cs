using Godot;

public partial class WormSegmentScript : EnemyScript
{
  public WormScript Worm;

  // the worm builds its segments itself on every peer, so no spawner removes them
  protected override bool FreedBySpawner => false;

  protected override void OnReady()
  {
    maxHealth = 300;
    explosionRadius = 2f;
    AddToGroup("worm_segments");
  }

  protected override void OnDied() => Worm?.OnSegmentDied(this);

  public void SetColor(Color color)
  {
    var mat = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
    GetNode<MeshInstance3D>("MeshInstance3D").MaterialOverride = mat;
  }
}
