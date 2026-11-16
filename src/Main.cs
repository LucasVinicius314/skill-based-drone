using Godot;

// the world scene's script: starts the session the menu asked for and spawns the drones. every peer builds a drone
// from the data the host spawns it with (peer id and position); the node name is the owning peer id
public partial class Main : Node3D
{
  const string MenuScene = "res://scenes/menu.tscn";
  const float SpawnSpacing = 4f;
  const double JoinTimeoutSeconds = 25;

  static readonly Vector3 SpawnOrigin = new(0.34f, 0.685f, 2.23f);

  readonly PackedScene _playerPrefab = GD.Load<PackedScene>("res://prefabs/player.tscn");

  NetSession _session;
  MultiplayerSpawner _playerSpawner;
  EnemySpawner _enemySpawner;
  Node _players;
  bool _started;
  bool _leaving;
  double _joinWait;
  CanvasLayer _joinOverlay;
  Label _joinLabel;
  int _spawnSlot;

  public override void _Ready()
  {
    _session = NetSession.Instance;
    _players = GetNode("Players");
    _playerSpawner = GetNode<MultiplayerSpawner>("PlayerSpawner");
    _enemySpawner = GetNode<EnemySpawner>("EnemySpawner");
    _playerSpawner.SpawnFunction = Callable.From<Variant, Node>(BuildPlayer);

    _session.PeerReady += SpawnPlayerFor;
    _session.PeerGone += DespawnPlayerFor;
    _session.CodeReady += OnRoomOpen;
    _session.Failed += OnFailed;
    _session.Ended += OnEnded;
    _session.StatusChanged += SetJoinStatus;

    var request = _session.Pending ?? new StartRequest(SessionKind.Solo);
    _session.Pending = null;

    switch (request.Kind)
    {
      case SessionKind.Client:
        BuildJoinOverlay(request.Code ?? "");
        _session.JoinRoom(request.Code ?? "");
        break;
      case SessionKind.Host:
        _session.StartSolo(); // offline until the room exists
        _session.OpenRoom(request.ListForFriends);
        break;
      default:
        _session.StartSolo();
        StartWorld();
        break;
    }
  }

  public override void _ExitTree()
  {
    _session.PeerReady -= SpawnPlayerFor;
    _session.PeerGone -= DespawnPlayerFor;
    _session.CodeReady -= OnRoomOpen;
    _session.Failed -= OnFailed;
    _session.Ended -= OnEnded;
    _session.StatusChanged -= SetJoinStatus;
  }

  // a client sees this until its own drone exists, so a join that stalls says so instead of showing an empty world
  void BuildJoinOverlay(string code)
  {
    _joinOverlay = new CanvasLayer { Layer = 50 };
    var background = new ColorRect { Color = new Color(0.04f, 0.06f, 0.05f, 1f) };
    background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    _joinOverlay.AddChild(background);

    _joinLabel = new Label
    {
      Text = $"Joining {code.Trim().ToUpperInvariant()}...",
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center,
    };
    _joinLabel.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.4f, 1f));
    _joinLabel.AddThemeFontSizeOverride("font_size", 22);
    _joinLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    _joinOverlay.AddChild(_joinLabel);
    AddChild(_joinOverlay);
  }

  void SetJoinStatus(string status)
  {
    if (_joinLabel != null && IsInstanceValid(_joinLabel))
    {
      _joinLabel.Text = status;
    }
  }

  public override void _Process(double delta)
  {
    if (_joinOverlay == null)
    {
      return;
    }

    if (PlayerScript.Local != null)
    {
      _joinOverlay.QueueFree();
      _joinOverlay = null;
      return;
    }

    _joinWait += delta;
    if (_joinWait > JoinTimeoutSeconds && !_leaving)
    {
      GD.PrintErr($"join stalled: peers={string.Join(",", Multiplayer.GetPeers())} me={Multiplayer.GetUniqueId()} players={_players.GetChildCount()} roster={_session.Roster.Count}");
      _session.LastError = Multiplayer.GetPeers().Length == 0
        ? "Could not reach the host. A strict network or firewall may be in the way."
        : "Connected to the host, but it never gave you a drone. Make sure you both run the same version.";
      BackToMenu();
    }
  }

  // the host's part of the world: its own drone and the enemies. clients get both from the spawners
  void StartWorld()
  {
    _started = true;
    SpawnPlayerFor(1);
    _enemySpawner.SpawnAll();
  }

  // a spawner only replicates once the room's multiplayer peer exists, so the hosting world starts here
  void OnRoomOpen(string code)
  {
    if (!_started)
    {
      StartWorld();
    }
  }

  // host only: a peer said hello, give it a drone
  void SpawnPlayerFor(int peer)
  {
    var position = SpawnOrigin + Vector3.Right * SpawnSpacing * _spawnSlot++;
    _playerSpawner.Spawn(new Godot.Collections.Array { peer, position });
  }

  void DespawnPlayerFor(int peer)
  {
    _players.GetNodeOrNull(peer.ToString())?.QueueFree();
  }

  Node BuildPlayer(Variant data)
  {
    var args = data.AsGodotArray();
    var peer = args[0].AsInt32();

    var player = _playerPrefab.Instantiate<PlayerScript>();
    player.Name = peer.ToString(); // the name is the owner: PlayerScript derives its authority from it
    player.Position = args[1].AsVector3();
    return player;
  }

  // joining failed, or a hosted room could not open: back to the menu with the reason
  void OnFailed(string message)
  {
    if (!_started || _session.Kind == SessionKind.Client)
    {
      BackToMenu();
    }
  }

  void OnEnded(string reason) => BackToMenu();

  void BackToMenu()
  {
    if (_leaving)
    {
      return;
    }

    _leaving = true;
    _session.Leave();
    GetTree().Paused = false;
    GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, MenuScene);
  }
}
