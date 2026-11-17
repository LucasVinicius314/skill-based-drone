using Godot;
using System;
using System.Collections.Generic;

// concrete compounds scattered over the map: a tower, a few bunkers and wall pieces each. the sites are a pure
// function of the terrain (fixed seed), so every peer builds the same ones and nothing goes over the network.
// the boxes sink into the ground, so they sit on slopes without floating
public partial class Structures : Node3D
{
  const int Seed = 4242;
  const int Wanted = 22;
  const float SiteRadius = 30f;
  const float MinSpacing = 160f;
  const float PadClearance = 220f;
  const float Sink = 8f;

  static readonly Lazy<List<Vector3>> _sites = new(FindSites);

  // the centre of every compound (y is the ground there)
  public static IReadOnlyList<Vector3> Sites => _sites.Value;

  // true when a ground position is inside a compound, so spawners keep clear of it
  public static bool Blocked(float x, float z, float margin = 0f)
  {
    var reach = SiteRadius + margin;
    foreach (var site in Sites)
    {
      var dx = site.X - x;
      var dz = site.Z - z;
      if (dx * dx + dz * dz < reach * reach)
      {
        return true;
      }
    }
    return false;
  }

  static List<Vector3> FindSites()
  {
    var rng = new Random(Seed);
    var sites = new List<Vector3>();
    var limit = Terrain.Half * 0.78f;

    for (var attempt = 0; attempt < 4000 && sites.Count < Wanted; attempt++)
    {
      var x = (float)(rng.NextDouble() * 2 - 1) * limit;
      var z = (float)(rng.NextDouble() * 2 - 1) * limit;
      var ground = Terrain.HeightAt(x, z);

      var pad = new Vector2(x, z).DistanceTo(Terrain.PadCenter);
      if (pad < PadClearance || ground > 30f || !IsFlat(x, z, ground))
      {
        continue;
      }

      var crowded = false;
      foreach (var other in sites)
      {
        if (new Vector2(other.X - x, other.Z - z).Length() < MinSpacing)
        {
          crowded = true;
          break;
        }
      }
      if (!crowded)
      {
        sites.Add(new Vector3(x, ground, z));
      }
    }
    return sites;
  }

  static bool IsFlat(float x, float z, float ground)
  {
    for (var i = 0; i < 8; i++)
    {
      var angle = i * Mathf.Tau / 8f;
      var h = Terrain.HeightAt(x + MathF.Cos(angle) * SiteRadius, z + MathF.Sin(angle) * SiteRadius);
      if (MathF.Abs(h - ground) > 7f)
      {
        return false;
      }
    }
    return true;
  }

  readonly StandardMaterial3D _concrete = new() { AlbedoColor = new Color(0.52f, 0.52f, 0.5f), Roughness = 1f };
  readonly StandardMaterial3D _roof = new() { AlbedoColor = new Color(0.26f, 0.28f, 0.3f), Roughness = 0.8f };

  public override void _Ready()
  {
    var rng = new Random(Seed + 1);
    for (var i = 0; i < Sites.Count; i++)
    {
      BuildCompound(i, Sites[i], rng);
    }
  }

  void BuildCompound(int index, Vector3 site, Random rng)
  {
    var root = new Node3D { Name = $"Compound{index}" };
    AddChild(root);
    root.Position = new Vector3(site.X, 0f, site.Z);
    root.Rotation = new Vector3(0f, (float)rng.NextDouble() * Mathf.Tau, 0f);

    var towerHeight = 20f + (float)rng.NextDouble() * 16f;
    AddBox(root, new Vector3(0f, 0f, 0f), new Vector3(8f, towerHeight, 8f), _concrete);
    AddBox(root, new Vector3(0f, 0f, 0f), new Vector3(13f, 1.2f, 13f), _roof, towerHeight);

    for (var i = 0; i < 3; i++)
    {
      var angle = i * Mathf.Tau / 3f + (float)rng.NextDouble() * 0.6f;
      var at = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * 21f;
      AddBox(root, at, new Vector3(14f, 5f, 9f), _concrete, 0f, -angle);
      AddBox(root, at, new Vector3(15f, 0.8f, 10f), _roof, 5f, -angle);
    }

    for (var i = 0; i < 2; i++)
    {
      var angle = Mathf.Pi * i + 0.9f;
      var at = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * 28f;
      AddBox(root, at, new Vector3(22f, 7f, 1.5f), _concrete, 0f, -angle + Mathf.Pi * 0.5f);
    }
  }

  // a box whose bottom follows the ground below it; `lift` raises it above that ground (a roof on a wall)
  void AddBox(Node3D root, Vector3 local, Vector3 size, StandardMaterial3D material, float lift = 0f, float yaw = 0f)
  {
    var world = root.ToGlobal(local);
    var ground = Terrain.HeightAt(world.X, world.Z);
    var sunk = lift > 0f ? 0f : Sink;

    var body = new StaticBody3D { Name = "Box" };
    root.AddChild(body);
    body.Position = new Vector3(local.X, ground + lift - sunk + (size.Y + sunk) * 0.5f, local.Z);
    body.Rotation = new Vector3(0f, yaw, 0f);

    var box = new Vector3(size.X, size.Y + sunk, size.Z);
    body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = box, Material = material } });
    body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = box } });
  }
}
