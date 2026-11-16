using Godot;
using System;

// developer command-line flags (everything after `--` on the Godot command line), used for scripted runs:
// screenshots and two-process multiplayer checks. nothing here changes normal play
public static class DevArgs
{
  static readonly string[] _args = OS.GetCmdlineUserArgs();

  public static bool Has(string name)
  {
    foreach (var arg in _args)
    {
      if (arg == $"--{name}" || arg.StartsWith($"--{name}=", StringComparison.Ordinal))
      {
        return true;
      }
    }
    return false;
  }

  public static string Get(string name)
  {
    foreach (var arg in _args)
    {
      if (arg.StartsWith($"--{name}=", StringComparison.Ordinal))
      {
        return arg[(name.Length + 3)..];
      }
    }
    return null;
  }

  public static int? GetInt(string name) => int.TryParse(Get(name), out var v) ? v : null;
}
