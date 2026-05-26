using System.Collections.Generic;
using Godot;

public partial class MissileScript : Node3D
{
  [Export] float speed = 25f;
  [Export] float lifetime = 6f;
  [Export] float splashRadius = 5f;
  [Export] int splashDamage = 3;
  [Export] float seekerRange = 30f;
  [Export] float turnRate = 3.5f;
  [Export] float armingDistance = 4f;

  readonly PackedScene _explosionPrefab = GD.Load<PackedScene>("res://prefabs/explosion_missile.tscn");

  float _elapsed;
  float _smokeTimer;
  float _distanceTravelled;
  Vector3 _velocity;
  Node3D _lockTarget;

  readonly Queue<Vector3> _trailPositions = new();
  const int TrailLength = 32;
  MeshInstance3D _trailMesh;
  ImmediateMesh _immediateMesh;

  public void Launch(Vector3 origin, Vector3 forward)
  {
    GlobalPosition = origin;
    LookAt(origin + forward, Vector3.Up);
    _velocity = forward * speed;
    BuildMesh();
    BuildTrail();

    var audio = new AudioStreamPlayer3D
    {
      Stream = GD.Load<AudioStream>("res://sounds/521377__jarusca__rocket-launch.mp3"),
      MaxDistance = 500f,
      Autoplay = true,
      VolumeDb = -10,
    };
    AddChild(audio);
    audio.Finished += audio.QueueFree;
  }

  void BuildMesh()
  {
    var body = new MeshInstance3D();
    var bodyMesh = new CapsuleMesh { Radius = 0.07f, Height = 0.5f };
    body.Mesh = bodyMesh;
    body.RotationDegrees = new Vector3(90, 0, 0);
    body.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.3f, 0.35f) };
    AddChild(body);

    var nose = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f } };
    nose.Position = new Vector3(0, 0, -0.25f);
    nose.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.2f, 0.1f) };
    AddChild(nose);

    var finMat = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.25f) };
    for (var i = 0; i < 4; i++)
    {
      var fin = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.01f, 0.12f, 0.14f) } };
      fin.MaterialOverride = finMat;
      fin.Position = new Vector3(0, 0, 0.18f);
      fin.RotationDegrees = new Vector3(0, 0, 45f * i);
      AddChild(fin);
    }
  }

  void BuildTrail()
  {
    _immediateMesh = new ImmediateMesh();
    _trailMesh = new MeshInstance3D
    {
      Mesh = _immediateMesh,
      MaterialOverride = new StandardMaterial3D
      {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoColor = new Color(0.9f, 0.5f, 0.1f),
      },
    };
    GetTree().CurrentScene.AddChild(_trailMesh);
  }

  void TryAcquireLock()
  {
    var best = float.MaxValue;
    Node3D found = null;

    foreach (var node in GetTree().GetNodesInGroup("targets"))
    {
      if (node is TargetScript t)
      {
        var dist = GlobalPosition.DistanceTo(t.GlobalPosition);
        if (dist < seekerRange && dist < best)
        {
          best = dist;
          found = t;
        }
      }
    }

    foreach (var node in GetTree().GetNodesInGroup("worm_segments"))
    {
      if (node is WormSegmentScript w)
      {
        var dist = GlobalPosition.DistanceTo(w.GlobalPosition);
        if (dist < seekerRange && dist < best)
        {
          best = dist;
          found = w;
        }
      }
    }

    _lockTarget = found;
  }

  public override void _PhysicsProcess(double delta)
  {
    _elapsed += (float)delta;
    if (_elapsed >= lifetime)
    {
      QueueFree();
      return;
    }

    if (_lockTarget == null || !IsInstanceValid(_lockTarget))
    {
      TryAcquireLock();
    }

    if (_lockTarget != null && IsInstanceValid(_lockTarget))
    {
      var toTarget = (_lockTarget.GlobalPosition - GlobalPosition).Normalized();
      var desired = toTarget * speed;
      _velocity = _velocity.Lerp(desired, turnRate * (float)delta);
      _velocity = _velocity.Normalized() * speed;
    }

    var move = _velocity * (float)delta;
    _distanceTravelled += move.Length();

    var result = new Godot.Collections.Dictionary();
    if (_distanceTravelled >= armingDistance)
    {
      var query = new PhysicsRayQueryParameters3D
      {
        From = GlobalPosition,
        To = GlobalPosition + move,
        CollideWithAreas = true,
        CollideWithBodies = true,
      };
      result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    }

    if (result.Count > 0)
    {
      var hitPos = result["position"].AsVector3();
      ApplySplash(hitPos);
      var explosion = _explosionPrefab.Instantiate<ExplosionScript>();
      explosion.Init(splashRadius);
      GetTree().CurrentScene.AddChild(explosion);
      explosion.GlobalPosition = hitPos;
      QueueFree();
      return;
    }

    GlobalPosition += move;
    LookAt(GlobalPosition + _velocity.Normalized(), Vector3.Up);

    _trailPositions.Enqueue(GlobalPosition);
    if (_trailPositions.Count > TrailLength)
    {
      _trailPositions.Dequeue();
    }
    DrawTrail();

    _smokeTimer -= (float)delta;
    if (_smokeTimer <= 0f)
    {
      _smokeTimer = 0.012f;
      SpawnSmokeParticle();
    }
  }

  void DrawTrail()
  {
    _immediateMesh.ClearSurfaces();
    if (_trailPositions.Count < 2) return;

    _immediateMesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
    var positions = _trailPositions.ToArray();
    for (var i = 0; i < positions.Length; i++)
    {
      var alpha = (float)i / positions.Length;
      _immediateMesh.SurfaceSetColor(new Color(1f, 0.5f, 0.1f, alpha));
      _immediateMesh.SurfaceAddVertex(positions[i]);
    }
    _immediateMesh.SurfaceEnd();
  }

  void SpawnSmokeParticle()
  {
    var rng = new RandomNumberGenerator();
    var drift = new Vector3(
      rng.RandfRange(-1.2f, 1.2f),
      rng.RandfRange(0.3f, 1.5f),
      rng.RandfRange(-1.2f, 1.2f)
    );
    var smoke = new SmokeParticleScript();
    smoke.Init(lifetime: 2.2f, startSize: 0.5f, endSize: 6f, drift: drift);
    GetTree().CurrentScene.AddChild(smoke);
    smoke.GlobalPosition = GlobalPosition + _velocity.Normalized() * -0.6f + new Vector3(
      rng.RandfRange(-0.05f, 0.05f),
      rng.RandfRange(-0.05f, 0.05f),
      rng.RandfRange(-0.05f, 0.05f)
    );
  }

  void ApplySplash(Vector3 hitPos)
  {
    foreach (var target in GetTree().GetNodesInGroup("targets"))
    {
      if (target is TargetScript t && t.GlobalPosition.DistanceTo(hitPos) <= splashRadius)
      {
        t.TakeDamage(splashDamage, hitPos);
      }
    }

    foreach (var segment in GetTree().GetNodesInGroup("worm_segments"))
    {
      if (segment is WormSegmentScript w && w.GlobalPosition.DistanceTo(hitPos) <= splashRadius)
      {
        w.TakeDamage(splashDamage);
      }
    }
  }

  public override void _ExitTree()
  {
    if (IsInstanceValid(_trailMesh)) _trailMesh.QueueFree();
  }
}
