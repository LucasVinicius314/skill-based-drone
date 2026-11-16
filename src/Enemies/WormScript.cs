using Godot;
using System.Collections.Generic;

public partial class WormScript : Node3D
{
  [Export] int segmentCount = 6;
  [Export] float segmentSpacing = 3.8f;
  [Export] float moveSpeed = 8f;
  [Export] float turnSpeed = 2f;
  [Export] float fireRate = 1.5f;

  // replicated from the host: where every segment is and how much health it has left (by original index, 0 = dead).
  // one synchronizer on the worm carries all of it, so the segments themselves need none
  [Export] public Vector3[] SyncPositions { get; set; } = [];
  [Export] public int[] SyncHealths { get; set; } = [];

  // every segment the worm was built with (dead ones stay until freed), and the live ones in chain order
  readonly List<WormSegmentScript> _all = new();
  readonly List<WormSegmentScript> _segments = new();
  readonly List<Vector3> _positionHistory = new();

  PlayerScript _player;
  float _targetTimer;
  float _fireTimer;

  readonly PackedScene _segmentPrefab = GD.Load<PackedScene>("res://prefabs/worm_segment.tscn");

  public override void _Ready()
  {
    for (var i = 0; i < segmentCount; i++)
    {
      var seg = _segmentPrefab.Instantiate<WormSegmentScript>();
      seg.Worm = this;
      seg.Name = $"Segment{i}"; // the same name on every peer, so a client's damage request finds it
      AddChild(seg);
      seg.GlobalPosition = GlobalPosition - GlobalTransform.Basis.Z * segmentSpacing * i;
      // head is brighter, tail fades darker
      var t = (float)i / segmentCount;
      seg.SetColor(new Color(0.2f, Mathf.Lerp(0.9f, 0.3f, t), 0.2f));
      _segments.Add(seg);
      _all.Add(seg);
    }
  }

  public override void _Process(double delta)
  {
    if (Multiplayer.IsServer())
    {
      Publish();
    }
    else
    {
      Follow();
    }
  }

  // host: write the chain into the replicated arrays
  void Publish()
  {
    if (SyncPositions.Length != _all.Count)
    {
      SyncPositions = new Vector3[_all.Count];
      SyncHealths = new int[_all.Count];
    }

    for (var i = 0; i < _all.Count; i++)
    {
      var alive = IsInstanceValid(_all[i]) && _all[i].Health > 0;
      SyncHealths[i] = alive ? _all[i].Health : 0;
      if (alive)
      {
        SyncPositions[i] = _all[i].GlobalPosition;
      }
    }

    // the property only replicates when it is set, not when an element changes
    SyncPositions = SyncPositions;
    SyncHealths = SyncHealths;
  }

  // client: hand each segment what the host says, and face each one along the chain
  void Follow()
  {
    if (SyncPositions.Length != _all.Count || SyncHealths.Length != _all.Count)
    {
      return;
    }

    for (var i = 0; i < _all.Count; i++)
    {
      var seg = _all[i];
      if (!IsInstanceValid(seg))
      {
        continue;
      }

      var dir = i == 0 ? SyncPositions[0] - SyncPositions[Mathf.Min(1, _all.Count - 1)] : SyncPositions[i - 1] - SyncPositions[i];
      seg.SyncPosition = SyncPositions[i];
      seg.SyncHealth = SyncHealths[i];
      if (dir.LengthSquared() > 0.001f && dir.Normalized().Cross(Vector3.Up).LengthSquared() > 0.001f)
      {
        seg.SyncRotation = Basis.LookingAt(dir.Normalized(), Vector3.Up).GetRotationQuaternion();
      }
    }
  }

  public override void _PhysicsProcess(double delta)
  {
    // only the host steers the worm; on a client the segments follow their replicated positions
    if (!Multiplayer.IsServer() || _segments.Count == 0)
    {
      return;
    }

    _targetTimer -= (float)delta;
    if (_targetTimer <= 0f || !IsInstanceValid(_player))
    {
      _targetTimer = 0.5f;
      _player = PlayerScript.Nearest(_segments[0].GlobalPosition);
    }
    if (_player == null)
    {
      return;
    }

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
      NetSession.Instance.EnemyBullet(_segments[0].GlobalPosition, forward, 1);
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
      // let the last segment's death reach the clients before the worm goes away
      GetTree().CreateTimer(0.6f).Timeout += () =>
      {
        if (IsInstanceValid(this))
        {
          QueueFree();
        }
      };
    }
  }
}
