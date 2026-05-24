using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BulletScript : Node3D
{
  public enum Owner { Player, Enemy }

  [Export] float speed = 80f;
  [Export] float lifetime = 3f;

  float _elapsed;
  Vector3 _velocity;
  Owner _owner;

  readonly Queue<Vector3> _trailPositions = new();
  const int TrailLength = 16;

  MeshInstance3D _trailMesh;
  ImmediateMesh _immediateMesh;
  StandardMaterial3D _trailMaterial;

  public void Launch(Vector3 origin, Vector3 forward, Owner owner = Owner.Player)
  {
    GlobalPosition = origin;
    _velocity = forward * speed;
    _owner = owner;

    _trailMaterial = new StandardMaterial3D
    {
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      VertexColorUseAsAlbedo = true,
      AlbedoColor = owner == Owner.Player ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.2f, 0.2f),
    };

    _immediateMesh = new ImmediateMesh();
    _trailMesh = new MeshInstance3D { Mesh = _immediateMesh, MaterialOverride = _trailMaterial };
    GetTree().CurrentScene.AddChild(_trailMesh);
  }

  public override void _PhysicsProcess(double delta)
  {
    _elapsed += (float)delta;
    if (_elapsed >= lifetime)
    {
      QueueFree();
      return;
    }

    var move = _velocity * (float)delta;
    var query = new PhysicsRayQueryParameters3D
    {
      From = GlobalPosition,
      To = GlobalPosition + move,
      CollideWithAreas = _owner == Owner.Player,
      CollideWithBodies = _owner == Owner.Enemy,
    };

    var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
    if (result.Count > 0)
    {
      var collider = result["collider"].As<Node>();
      if (_owner == Owner.Player)
      {
        collider.GetParentOrNull<TargetScript>()?.TakeDamage(1, result["position"].AsVector3());
      }
      else
      {
        (collider as PlayerScript ?? collider.GetParentOrNull<PlayerScript>())?.TakeDamage(1);
      }
      QueueFree();
      return;
    }

    GlobalPosition += move;

    _trailPositions.Enqueue(GlobalPosition);
    if (_trailPositions.Count > TrailLength)
    {
      _trailPositions.Dequeue();
    }

    DrawTrail();
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
      _immediateMesh.SurfaceSetColor(new Color(1, 1, 1, alpha));
      _immediateMesh.SurfaceAddVertex(positions[i]);
    }
    _immediateMesh.SurfaceEnd();
  }

  public override void _ExitTree()
  {
    if (IsInstanceValid(_trailMesh)) _trailMesh.QueueFree();
  }
}
