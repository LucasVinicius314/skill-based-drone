using Godot;

// spawns the projectiles that every peer replays locally: enemy shots and flares. enemy shots are real on every peer,
// but only the peer whose drone they hit takes the damage; enemy missiles only damage enemies on the host
public static class Projectiles
{
  static readonly PackedScene _bulletPrefab = GD.Load<PackedScene>("res://prefabs/bullet.tscn");
  static readonly PackedScene _missilePrefab = GD.Load<PackedScene>("res://prefabs/missile.tscn");

  public static void SpawnEnemyBullet(Node context, Vector3 origin, Vector3 forward, int damage)
  {
    var bullet = _bulletPrefab.Instantiate<BulletScript>();
    context.GetTree().CurrentScene.AddChild(bullet);
    bullet.Launch(origin, forward, BulletScript.Faction.Enemy, damage);
  }

  public static void SpawnEnemyMissile(Node context, Vector3 origin, Vector3 forward, int targetPeer)
  {
    var missile = _missilePrefab.Instantiate<MissileScript>();
    missile.Authoritative = context.Multiplayer.IsServer();
    context.GetTree().CurrentScene.AddChild(missile);
    missile.Launch(origin, forward, PlayerScript.ByPeer(targetPeer));
  }

  public static void SpawnFlare(Node context, Vector3 origin, Vector3 velocity)
  {
    var flare = new FlareScript();
    context.GetTree().CurrentScene.AddChild(flare);
    flare.Launch(origin, velocity);
  }
}
