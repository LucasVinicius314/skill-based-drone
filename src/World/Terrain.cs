using Godot;
using System;
using System.Threading.Tasks;

// the ground: a procedural heightfield built in square chunks when the world starts. the height is a pure function of
// (x, z) with fixed constants, so every peer builds the identical terrain and nothing about it goes over the network.
// a flat pad around the origin is where the drones start; hills grow outward and a ring of mountains closes the map
public partial class Terrain : Node3D
{
  public const float Size = 1024f;

  const float Half = Size * 0.5f;
  const float Cell = 2f;
  const int ChunkCells = 64;
  const int Cells = (int)(Size / Cell);
  const int Chunks = Cells / ChunkCells;
  const int Stride = Cells + 1;

  const int Seed = 1337;
  const float FlatHeight = -1f;
  const float FlatRadius = 45f;
  const float FlatBlend = 120f;
  const float HillHeight = 30f;
  const float RidgeHeight = 22f;
  const float RimHeight = 80f;

  static readonly Color Grass = new(0.20f, 0.36f, 0.12f);
  static readonly Color GrassLight = new(0.34f, 0.46f, 0.16f);
  static readonly Color Dirt = new(0.45f, 0.36f, 0.25f);
  static readonly Color Rock = new(0.43f, 0.41f, 0.39f);
  static readonly Color Snow = new(0.92f, 0.94f, 0.96f);

  // the height of the ground at a world position, usable before the terrain node exists
  public static float HeightAt(float x, float z)
  {
    var distance = MathF.Sqrt(x * x + z * z);
    var open = Smooth((distance - FlatRadius) / FlatBlend);

    var hills = Fbm(x * 0.0035f, z * 0.0035f, Seed, 5);
    var ridge = 1f - MathF.Abs(Fbm(x * 0.0022f, z * 0.0022f, Seed + 1, 3));
    var land = hills * HillHeight + ridge * ridge * RidgeHeight;

    var edge = MathF.Max(MathF.Abs(x), MathF.Abs(z)) / Half;
    var rim = Smooth((edge - 0.78f) / 0.22f) * RimHeight;

    return FlatHeight + open * land + rim;
  }

  public override void _Ready()
  {
    var heights = SampleHeights();
    var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/terrain.gdshader") };

    for (var cz = 0; cz < Chunks; cz++)
    {
      for (var cx = 0; cx < Chunks; cx++)
      {
        AddChunk(cx, cz, heights, material);
      }
    }
  }

  static float[] SampleHeights()
  {
    var heights = new float[Stride * Stride];
    Parallel.For(0, Stride, j =>
    {
      for (var i = 0; i < Stride; i++)
      {
        heights[j * Stride + i] = HeightAt(-Half + i * Cell, -Half + j * Cell);
      }
    });
    return heights;
  }

  void AddChunk(int cx, int cz, float[] heights, ShaderMaterial material)
  {
    const int side = ChunkCells + 1;
    var vertices = new Vector3[side * side];
    var normals = new Vector3[side * side];
    var colors = new Color[side * side];
    var indices = new int[ChunkCells * ChunkCells * 6];

    var originX = -Half + cx * ChunkCells * Cell;
    var originZ = -Half + cz * ChunkCells * Cell;

    for (var j = 0; j < side; j++)
    {
      for (var i = 0; i < side; i++)
      {
        var gi = cx * ChunkCells + i;
        var gj = cz * ChunkCells + j;
        var height = heights[gj * Stride + gi];

        // normals come from the whole grid, so neighbouring chunks agree along their shared edge
        var left = heights[gj * Stride + Math.Max(gi - 1, 0)];
        var right = heights[gj * Stride + Math.Min(gi + 1, Cells)];
        var back = heights[Math.Max(gj - 1, 0) * Stride + gi];
        var front = heights[Math.Min(gj + 1, Cells) * Stride + gi];
        var normal = new Vector3(left - right, 2f * Cell, back - front).Normalized();

        var index = j * side + i;
        vertices[index] = new Vector3(i * Cell, height, j * Cell);
        normals[index] = normal;
        colors[index] = ColorAt(originX + i * Cell, originZ + j * Cell, height, normal);
      }
    }

    var t = 0;
    for (var j = 0; j < ChunkCells; j++)
    {
      for (var i = 0; i < ChunkCells; i++)
      {
        var a = j * side + i;
        var b = a + 1;
        var c = a + side;
        var d = c + 1;

        // clockwise seen from above, which is the front face in Godot
        indices[t++] = a;
        indices[t++] = b;
        indices[t++] = c;
        indices[t++] = b;
        indices[t++] = d;
        indices[t++] = c;
      }
    }

    var arrays = new Godot.Collections.Array();
    arrays.Resize((int)Mesh.ArrayType.Max);
    arrays[(int)Mesh.ArrayType.Vertex] = vertices;
    arrays[(int)Mesh.ArrayType.Normal] = normals;
    arrays[(int)Mesh.ArrayType.Color] = colors;
    arrays[(int)Mesh.ArrayType.Index] = indices;

    var mesh = new ArrayMesh();
    mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    mesh.SurfaceSetMaterial(0, material);

    var chunk = new MeshInstance3D
    {
      Name = $"Chunk_{cx}_{cz}",
      Mesh = mesh,
      CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };
    AddChild(chunk);
    chunk.Position = new Vector3(originX, 0f, originZ);

    var body = new StaticBody3D();
    chunk.AddChild(body);
    body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
  }

  static Color ColorAt(float x, float z, float height, Vector3 normal)
  {
    var patch = Fbm(x * 0.03f, z * 0.03f, Seed + 2, 3) * 0.5f + 0.5f;
    var color = Grass.Lerp(GrassLight, Math.Clamp(patch, 0f, 1f));

    // bare ground on the low flat pad
    color = color.Lerp(Dirt, Math.Clamp(1f - (height + 1f) / 5f, 0f, 1f));

    // steep slopes are rock, high ground is snow
    var steep = Smooth((1f - normal.Y - 0.22f) / 0.3f);
    color = color.Lerp(Rock, steep);

    var snow = Smooth((height + patch * 8f - 52f) / 16f) * (1f - steep * 0.6f);
    return color.Lerp(Snow, snow);
  }

  // ------------------------------------------------------------------ noise
  // plain C# so the terrain does not depend on engine objects and is the same on every machine

  static float Smooth(float t)
  {
    t = Math.Clamp(t, 0f, 1f);
    return t * t * (3f - 2f * t);
  }

  static float Fbm(float x, float y, int seed, int octaves)
  {
    var sum = 0f;
    var amplitude = 1f;
    var total = 0f;
    for (var o = 0; o < octaves; o++)
    {
      sum += Perlin(x, y, seed + o) * amplitude;
      total += amplitude;
      amplitude *= 0.5f;
      x *= 2f;
      y *= 2f;
    }
    return sum / total * 1.6f;
  }

  static float Perlin(float x, float y, int seed)
  {
    var x0 = (int)MathF.Floor(x);
    var y0 = (int)MathF.Floor(y);
    var fx = x - x0;
    var fy = y - y0;
    var u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
    var v = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);

    var n00 = Gradient(x0, y0, fx, fy, seed);
    var n10 = Gradient(x0 + 1, y0, fx - 1f, fy, seed);
    var n01 = Gradient(x0, y0 + 1, fx, fy - 1f, seed);
    var n11 = Gradient(x0 + 1, y0 + 1, fx - 1f, fy - 1f, seed);
    return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v);
  }

  static float Gradient(int ix, int iy, float dx, float dy, int seed)
  {
    unchecked
    {
      var h = ix * 374761393 + iy * 668265263 + seed * 1442695041;
      h = (h ^ (h >> 13)) * 1274126177;
      h ^= h >> 16;
      var angle = (h & 0xFFFF) / 65535f * MathF.Tau;
      return MathF.Cos(angle) * dx + MathF.Sin(angle) * dy;
    }
  }
}
