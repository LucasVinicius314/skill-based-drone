using Godot;
using System.Collections.Generic;

public partial class WormScript : Node3D
{
  [Export] int segmentCount = 6;
  [Export] float segmentSpacing = 3.8f;
  [Export] float moveSpeed = 8f;
  [Export] float turnSpeed = 2f;
  [Export] float fireRate = 1.5f;

  readonly List<WormSegmentScript> _segments = new();
  readonly List<Vector3> _positionHistory = new();

  PlayerScript _player;
  float _fireTimer;

  readonly PackedScene _segmentPrefab = GD.Load<PackedScene>("res://prefabs/worm_segment.tscn");
  readonly PackedScene _bulletPrefab = GD.Load<PackedScene>("res://prefabs/bullet.tscn");

  public override void _Ready()
  {
    _player = GetTree().CurrentScene.GetNode<PlayerScript>("Player");

    for (var i = 0; i < segmentCount; i++)
    {
      var seg = _segmentPrefab.Instantiate<WormSegmentScript>();
      seg.Worm = this;
      AddChild(seg);
      seg.GlobalPosition = GlobalPosition - GlobalTransform.Basis.Z * segmentSpacing * i;
      // head is brighter, tail fades darker
      var t = (float)i / segmentCount;
      seg.SetColor(new Color(0.2f, Mathf.Lerp(0.9f, 0.3f, t), 0.2f));
      _segments.Add(seg);
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    if (_segments.Count == 0 || _player == null) return;

    var head = _segments[0];

    // steer head toward player
    var toPlayer = (_player.GlobalPosition - head.GlobalPosition).Normalized();
    var currentDir = -head.GlobalTransform.Basis.Z;
    var newDir = currentDir.Lerp(toPlayer, turnSpeed * (float)delta).Normalized();
    head.GlobalPosition += newDir * moveSpeed * (float)delta;
    if (newDir.LengthSquared() > 0.001f)
    {
      head.LookAt(head.GlobalPosition + newDir, Vector3.Up);
    }

    // record head position history for tail following
    _positionHistory.Insert(0, head.GlobalPosition);
    var maxHistory = Mathf.CeilToInt(segmentSpacing / (moveSpeed * (float)delta) * segmentCount) + 10;
    if (_positionHistory.Count > maxHistory)
    {
      _positionHistory.RemoveAt(_positionHistory.Count - 1);
    }

    _fireTimer -= (float)delta;
    if (_fireTimer <= 0f)
    {
      _fireTimer = fireRate;
      var forward = EnemyScript.AddSpread((_player.GlobalPosition - _segments[0].GlobalPosition).Normalized(), 0.2f);
      var bullet = _bulletPrefab.Instantiate<BulletScript>();
      GetTree().CurrentScene.AddChild(bullet);
      bullet.Launch(_segments[0].GlobalPosition, forward, BulletScript.Faction.Enemy);
    }

    // each segment follows the path at a fixed spacing
    for (var i = 1; i < _segments.Count; i++)
    {
      if (!IsInstanceValid(_segments[i])) continue;
      var targetPos = GetPositionAlongHistory(i * segmentSpacing);
      var seg = _segments[i];
      var dir = (targetPos - seg.GlobalPosition).Normalized();
      seg.GlobalPosition = targetPos;
      if (dir.LengthSquared() > 0.001f)
      {
        seg.LookAt(seg.GlobalPosition + dir, Vector3.Up);
      }
    }
  }

  Vector3 GetPositionAlongHistory(float distance)
  {
    if (_positionHistory.Count == 0) return GlobalPosition;
    var traveled = 0f;
    for (var i = 1; i < _positionHistory.Count; i++)
    {
      var step = _positionHistory[i - 1].DistanceTo(_positionHistory[i]);
      if (traveled + step >= distance)
      {
        var t = (distance - traveled) / step;
        return _positionHistory[i - 1].Lerp(_positionHistory[i], t);
      }
      traveled += step;
    }
    return _positionHistory[^1];
  }

  public void OnSegmentDied(WormSegmentScript segment)
  {
    _segments.Remove(segment);
    if (_segments.Count == 0)
    {
      QueueFree();
    }
  }
}
