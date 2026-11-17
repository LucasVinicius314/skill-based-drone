using Godot;

// which weapon sits in each of the drone's three slots (primary on left mouse, secondary on right mouse, tertiary on
// mouse button 5). the choice is made in the main menu and kept in user://loadout.cfg. other peers learn it from the
// drone's replicated `SyncLoadout`, so they can build the same weapons for the shots they replay
public static class Loadout
{
  public const int SlotCount = 3;

  public static readonly string[] SlotNames = { "PRIMARY", "SECONDARY", "TERTIARY" };

  // where each slot's weapon sits on the drone's Mount
  public static readonly Vector3[] Mounts =
  {
    new(0f, -0.203f, 0.176f),
    new(0.218f, -0.184f, 0.152f),
    new(-0.218f, -0.184f, 0.152f),
  };

  public static readonly (string Name, PackedScene Prefab)[] Catalog =
  {
    (".50 CAL", GD.Load<PackedScene>("res://prefabs/weapon_50cal.tscn")),
    ("MISSILE LAUNCHER", GD.Load<PackedScene>("res://prefabs/weapon_missile_launcher.tscn")),
    ("MINIGUN", GD.Load<PackedScene>("res://prefabs/weapon_minigun.tscn")),
    ("DUAL 20MM CANNON", GD.Load<PackedScene>("res://prefabs/weapon_dual20.tscn")),
    ("M61 VULCAN 20MM", GD.Load<PackedScene>("res://prefabs/weapon_vulcan20.tscn")),
  };

  const string File = "user://loadout.cfg";

  public static int[] Slots { get; } = { 0, 1, 4 };

  static Loadout()
  {
    var config = new ConfigFile();
    if (config.Load(File) != Error.Ok)
    {
      return;
    }

    for (var i = 0; i < SlotCount; i++)
    {
      var choice = (int)config.GetValue("loadout", $"slot{i}", Slots[i]);
      Slots[i] = choice >= 0 && choice < Catalog.Length ? choice : Slots[i];
    }
  }

  public static void Set(int slot, int choice)
  {
    Slots[slot] = choice;

    var config = new ConfigFile();
    for (var i = 0; i < SlotCount; i++)
    {
      config.SetValue("loadout", $"slot{i}", Slots[i]);
    }
    config.Save(File);
  }

  // one int that carries the three choices (0 = unknown, so each is stored plus one)
  public static int Pack(int[] slots) => (slots[0] + 1) | ((slots[1] + 1) << 8) | ((slots[2] + 1) << 16);

  public static int[] Unpack(int packed)
  {
    var slots = new int[SlotCount];
    for (var i = 0; i < SlotCount; i++)
    {
      var choice = ((packed >> (8 * i)) & 0xFF) - 1;
      slots[i] = choice >= 0 && choice < Catalog.Length ? choice : 0;
    }
    return slots;
  }
}
