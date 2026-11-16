using Godot;
using P2PSignaling;
using System;
using System.Collections.Generic;

public enum SessionKind
{
  Solo,
  Host,
  Client,
}

// what the menu (or a dev flag) asked for; the world scene reads it when it loads
public sealed record StartRequest(SessionKind Kind, string Code = null, bool ListForFriends = true);

// autoload at /root/NetSession (the same path on every peer, which RPCs need). owns the p2p lobby and speaks Godot's
// high-level multiplayer on top of it. the host is peer 1 and the source of truth for enemies; every peer owns and
// simulates its own drone. this node carries the session messages (handshake, roster, ping) and the one-off effects
// (shots, flares) that every peer replays locally.
//
// wiring follows libs/p2p-signaling-server/docs/integration-guide.md section 6: the peer is assigned in PeerCreated,
// RPCs only go out after ConnectedToServer / PeerConnected, and only the host calls host-to-client RPCs
public partial class NetSession : Node
{
  public const string Bucket = "skill-based-drone";

  // bump when the handshake or RPC contract changes incompatibly
  public const int Protocol = 1;

  const float PingIntervalSeconds = 1f;
  const float MaxShotSpeed = 1500f;

  public static NetSession Instance { get; private set; }

  public StartRequest Pending { get; set; }
  public SessionKind Kind { get; private set; } = SessionKind.Solo;
  public string PlayerName { get; set; } = "player";
  public string RoomCode { get; private set; }

  // why the last session ended or failed, for the menu to show once after returning to it
  public string LastError { get; set; }

  public bool IsOnline => Kind != SessionKind.Solo;
  public bool IsHost => Kind != SessionKind.Client;

  // peer id to display name for everyone in the room, the host (1) included. kept in step on every peer
  public IReadOnlyDictionary<int, string> Roster => _roster;

  // menu / HUD feedback
  public event Action<string> StatusChanged;
  public event Action<string> CodeReady;
  public event Action<IReadOnlyList<FriendRoom>> FriendRoomsReceived;
  public event Action<string> Failed;
  public event Action<string> Ended;
  public event Action RosterChanged;

  // world wiring
  public event Action<int> PeerReady;
  public event Action<int> PeerGone;

  P2PLobby _lobby;
  bool _retriedWithoutFriends;

  readonly Dictionary<int, int> _pings = new();
  readonly Dictionary<int, string> _names = new();
  readonly Dictionary<int, string> _roster = new();
  readonly HashSet<int> _synced = new();
  float _pingTimer;
  int _lastPing = -1;

  public override void _EnterTree()
  {
    Instance = this;
  }

  public override void _Ready()
  {
    Multiplayer.ConnectedToServer += OnConnectedToServer;
    Multiplayer.PeerConnected += id => GD.Print($"peer {id} connected");
    Multiplayer.PeerDisconnected += OnPeerDisconnected;
    Multiplayer.ServerDisconnected += () => End("the host left");

    PlayerName = DevArgs.Get("net-name") ?? PlayerProfile.Name;

    // the game has no server address of its own: the library reads P2P_SIGNALING_URL or ~/.p2p-signaling-server/server.
    // for scripted runs --net-url points this process at another server (the local dev server) without touching that file
    if (DevArgs.Get("net-url") is { } devUrl)
    {
      ServerSettings.OverrideForThisRun(devUrl);
    }

    if (DevArgs.Has("net-host"))
    {
      Pending = new StartRequest(SessionKind.Host, ListForFriends: !DevArgs.Has("net-no-friends"));
    }
    else if (DevArgs.Get("net-join") is { } code)
    {
      Pending = new StartRequest(SessionKind.Client, code);
    }
    else if (DevArgs.Has("solo"))
    {
      Pending = new StartRequest(SessionKind.Solo);
    }

    if (DevArgs.Has("net-report-at") || DevArgs.Has("net-quit-at") || DevArgs.Has("screenshot") || DevArgs.Has("menu-screenshot"))
    {
      AddChild(new NetDevHooks());
    }
  }

  // the version reported to the signaling server (informational): the release tag without its leading v, read from the
  // version.txt that release builds carry next to the executable, "dev" when run from source
  public static string GameVersion()
  {
    try
    {
      var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "version.txt");
      if (System.IO.File.Exists(path))
      {
        return System.IO.File.ReadAllText(path).Trim().TrimStart('v');
      }
    }
    catch (System.IO.IOException)
    {
      // unreadable: fall through
    }
    return "dev";
  }

  public int PingOf(int peer) => _pings.TryGetValue(peer, out var ms) ? ms : -1;

  public string NameOf(int peer) => _roster.TryGetValue(peer, out var n) ? n : _names.TryGetValue(peer, out var m) ? m : $"peer {peer}";

  // ------------------------------------------------------------------ lobby

  P2PLobby EnsureLobby()
  {
    if (_lobby != null)
    {
      return _lobby;
    }

    // Url stays empty on purpose: the library then uses the machine's configured server and never saves one of ours
    _lobby = new P2PLobby("", Bucket, GameVersion()) { PlayerName = PlayerName };
    AddChild(_lobby); // it must be in the tree: it does its work in _Process

    // rule 1: attach the multiplayer peer the moment it exists, before any link is up
    _lobby.PeerCreated += peer => Multiplayer.MultiplayerPeer = peer;
    _lobby.CodeReady += code =>
    {
      RoomCode = code;
      GD.Print($"CODE {code}");
      CodeReady?.Invoke(code);
    };
    _lobby.FriendRoomsReceived += rooms => FriendRoomsReceived?.Invoke(rooms);
    _lobby.Failed += OnLobbyFailed;
    _lobby.RoomClosed += reason => End($"room closed: {reason}");
    _lobby.SignalingLost += reason =>
    {
      GD.Print($"signaling lost ({reason}); linked peers keep playing");
      RoomCode = null;
      StatusChanged?.Invoke("Lost the signaling server: nobody new can join this room.");
    };
    return _lobby;
  }

  // the friends-list part of the lobby: used by the menu before any room exists
  public void RefreshFriendRooms()
  {
    var lobby = EnsureLobby();
    lobby.PlayerName = PlayerName;
    lobby.ListFriendRooms();
  }

  public IReadOnlyList<string> FriendFileProblems => EnsureLobby().FriendFileProblems;

  // host an online room (the world is already running); friends who have your id can see it
  public void OpenRoom(bool listForFriends)
  {
    _lobby?.Leave(); // drop the menu's browsing connection so the room starts from a clean lobby
    var lobby = EnsureLobby();
    Kind = SessionKind.Host;
    _retriedWithoutFriends = !listForFriends;
    lobby.PlayerName = PlayerName;
    lobby.ListForFriends = listForFriends;
    StatusChanged?.Invoke("Opening room...");
    lobby.CreateRoom();
  }

  public void JoinRoom(string code)
  {
    _lobby?.Leave();
    var lobby = EnsureLobby();
    Kind = SessionKind.Client;
    lobby.PlayerName = PlayerName;
    RoomCode = code.Trim().ToUpperInvariant();
    StatusChanged?.Invoke($"Joining {RoomCode}...");
    lobby.JoinRoom(RoomCode);
  }

  public void StartSolo()
  {
    Kind = SessionKind.Solo;
    EnsureOfflinePeer();
    RebuildRoster();
  }

  void OnLobbyFailed(string code, string message)
  {
    // a server older than the friends feature answers bad_message to a friends-listed room: host without the listing
    if (Kind == SessionKind.Host && !_retriedWithoutFriends && code == "bad_message")
    {
      _retriedWithoutFriends = true;
      _lobby.ListForFriends = false;
      _lobby.CreateRoom();
      return;
    }

    GD.PrintErr($"net failed: {code}: {message}");
    LastError = FriendlyFailure(code, message);
    Failed?.Invoke(LastError);
  }

  // plain-language versions of the library's failure codes (see the integration guide's troubleshooting table)
  static string FriendlyFailure(string code, string message)
  {
    return code switch
    {
      "no_server_url" => "No server address is set. Open Settings and enter the server address.",
      "room_not_found" => "Room not found. Check the code; the host may have left.",
      "room_full" => "That room is full.",
      "room_locked" => "The host is not accepting new players right now.",
      "connect_timeout" => "Could not connect to the host. A strict network or firewall may be in the way.",
      "bucket_not_found" or "bucket_disabled" => "The server does not have this game enabled.",
      "rate_limited" or "locked_out" => "Too many attempts. Wait a minute and try again.",
      "server_restarting" => "The server is restarting. Try again in a moment.",
      "signaling_closed" or "connect_failed" => "Could not reach the server. Check the address in Settings and your connection.",
      "webrtc_unavailable" => "This build is missing the WebRTC component (addons/webrtc_native).",
      _ => $"{message} ({code})",
    };
  }

  // tear the session down and return to offline state. the caller changes scenes
  public void Leave()
  {
    _lobby?.Leave();
    EnsureOfflinePeer(true);
    ResetState();
  }

  // offline play needs an OfflineMultiplayerPeer: with no peer at all, spawners refuse to spawn and RPCs fail
  void EnsureOfflinePeer(bool replace = false)
  {
    if (replace || !Multiplayer.HasMultiplayerPeer())
    {
      Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
    }
  }

  void End(string reason)
  {
    GD.Print($"session ended: {reason}");
    LastError = char.ToUpperInvariant(reason[0]) + reason[1..] + ".";
    Leave();
    Ended?.Invoke(reason);
  }

  void ResetState()
  {
    Kind = SessionKind.Solo;
    RoomCode = null;
    _names.Clear();
    _synced.Clear();
    _roster.Clear();
    _pings.Clear();
    _lastPing = -1;
  }

  // ------------------------------------------------------------------ ping

  public override void _Process(double delta)
  {
    if (Kind == SessionKind.Solo)
    {
      return;
    }

    _pingTimer -= (float)delta;
    if (_pingTimer > 0f)
    {
      return;
    }
    _pingTimer = PingIntervalSeconds;

    if (Kind == SessionKind.Client)
    {
      // only once the host has put us in the roster; the previous measurement rides along so the host knows it too
      if (_roster.Count > 0)
      {
        RpcId(1, MethodName.RpcPing, (long)Time.GetTicksMsec(), _lastPing);
      }
      return;
    }

    _pings[1] = 0;
    var wire = new Godot.Collections.Dictionary();
    foreach (var (peer, ms) in _pings)
    {
      wire[peer] = ms;
    }
    foreach (var peer in _synced)
    {
      RpcId(peer, MethodName.RpcPings, wire);
    }
  }

  // ------------------------------------------------------------------ link events

  void OnConnectedToServer()
  {
    GD.Print("client: linked to the host");
    StatusChanged?.Invoke("Linked. Joining the world...");
    RpcId(1, MethodName.RpcHello, PlayerName, Protocol);
  }

  void OnPeerDisconnected(long id)
  {
    var peer = (int)id;
    GD.Print($"peer {peer} disconnected");
    if (!IsHost)
    {
      return;
    }

    _names.Remove(peer);
    _pings.Remove(peer);
    if (_synced.Remove(peer))
    {
      RebuildRoster();
      PeerGone?.Invoke(peer);
    }
  }

  // ------------------------------------------------------------------ RPCs: client to host

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcHello(string playerName, int protocol)
  {
    if (!Multiplayer.IsServer())
    {
      return;
    }

    var sender = Multiplayer.GetRemoteSenderId();
    if (protocol != Protocol)
    {
      RpcId(sender, MethodName.RpcRejected, $"version mismatch (host {Protocol}, you {protocol})");
      return;
    }

    _names[sender] = Sanitize(playerName);
    GD.Print($"host: hello from {_names[sender]} (peer {sender})");
    if (_synced.Add(sender))
    {
      RebuildRoster();
      PeerReady?.Invoke(sender);
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcPing(long sentMsec, int reportedPing)
  {
    var sender = Multiplayer.GetRemoteSenderId();
    if (!Multiplayer.IsServer() || !_synced.Contains(sender))
    {
      return;
    }

    if (reportedPing >= 0)
    {
      _pings[sender] = Math.Min(reportedPing, 9999);
    }
    RpcId(sender, MethodName.RpcPong, sentMsec);
  }

  // a client fired a player weapon: the host replays it and tells everyone else
  [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcShotRequest(int weaponIndex, Vector3 origin, Vector3 forward)
  {
    var sender = Multiplayer.GetRemoteSenderId();
    if (!Multiplayer.IsServer() || !_synced.Contains(sender) || !IsSaneShot(origin, forward))
    {
      return;
    }

    ReplayShot(sender, weaponIndex, origin, forward);
    foreach (var peer in _synced)
    {
      if (peer != sender)
      {
        RpcId(peer, MethodName.RpcShot, sender, weaponIndex, origin, forward);
      }
    }
  }

  [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcFlareRequest(Vector3 origin, Vector3 velocity)
  {
    var sender = Multiplayer.GetRemoteSenderId();
    if (!Multiplayer.IsServer() || !_synced.Contains(sender) || velocity.Length() > MaxShotSpeed)
    {
      return;
    }

    Projectiles.SpawnFlare(this, origin, velocity);
    foreach (var peer in _synced)
    {
      if (peer != sender)
      {
        RpcId(peer, MethodName.RpcFlare, origin, velocity);
      }
    }
  }

  // ------------------------------------------------------------------ RPCs: host to client (Authority mode)

  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcRoster(Godot.Collections.Dictionary received)
  {
    _roster.Clear();
    foreach (var key in received.Keys)
    {
      _roster[key.AsInt32()] = received[key].AsString();
    }
    RosterChanged?.Invoke();
  }

  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcPong(long sentMsec)
  {
    var rtt = (int)Math.Clamp((long)Time.GetTicksMsec() - sentMsec, 0, 9999);

    // a little smoothing so the number does not flicker
    _lastPing = _lastPing < 0 ? rtt : (int)Math.Round(_lastPing * 0.6 + rtt * 0.4);
    _pings[Multiplayer.GetUniqueId()] = _lastPing;
  }

  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcPings(Godot.Collections.Dictionary received)
  {
    var own = Multiplayer.GetUniqueId();
    var mine = _pings.TryGetValue(own, out var m) ? m : -1;

    _pings.Clear();
    foreach (var key in received.Keys)
    {
      _pings[key.AsInt32()] = received[key].AsInt32();
    }
    if (mine >= 0)
    {
      _pings[own] = mine; // our own measurement is fresher than what the host last heard
    }
  }

  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcRejected(string reason)
  {
    LastError = $"The host refused you: {reason}.";
    Failed?.Invoke(LastError);
  }

  // another player fired: replay it here, harmless to enemies (only the shooter's own copy deals damage)
  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcShot(int shooter, int weaponIndex, Vector3 origin, Vector3 forward)
  {
    ReplayShot(shooter, weaponIndex, origin, forward);
  }

  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcFlare(Vector3 origin, Vector3 velocity)
  {
    Projectiles.SpawnFlare(this, origin, velocity);
  }

  // an enemy fired: every peer replays it, and only the peer whose drone it hits takes the damage
  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcEnemyBullet(Vector3 origin, Vector3 forward, int damage)
  {
    Projectiles.SpawnEnemyBullet(this, origin, forward, damage);
  }

  [Rpc(TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
  void RpcEnemyMissile(Vector3 origin, Vector3 forward, int targetPeer)
  {
    Projectiles.SpawnEnemyMissile(this, origin, forward, targetPeer);
  }

  // ------------------------------------------------------------------ effects other code calls

  // the local player fired weapon weaponIndex: tell the others (the shot itself already exists locally)
  public void AnnounceShot(int weaponIndex, Vector3 origin, Vector3 forward)
  {
    if (Kind == SessionKind.Client)
    {
      RpcId(1, MethodName.RpcShotRequest, weaponIndex, origin, forward);
    }
    else if (Kind == SessionKind.Host)
    {
      foreach (var peer in _synced)
      {
        RpcId(peer, MethodName.RpcShot, 1, weaponIndex, origin, forward);
      }
    }
  }

  public void AnnounceFlare(Vector3 origin, Vector3 velocity)
  {
    if (Kind == SessionKind.Client)
    {
      RpcId(1, MethodName.RpcFlareRequest, origin, velocity);
    }
    else if (Kind == SessionKind.Host)
    {
      foreach (var peer in _synced)
      {
        RpcId(peer, MethodName.RpcFlare, origin, velocity);
      }
    }
  }

  // host only: an enemy fired a bullet or missile
  public void EnemyBullet(Vector3 origin, Vector3 forward, int damage)
  {
    Projectiles.SpawnEnemyBullet(this, origin, forward, damage);
    foreach (var peer in _synced)
    {
      RpcId(peer, MethodName.RpcEnemyBullet, origin, forward, damage);
    }
  }

  public void EnemyMissile(Vector3 origin, Vector3 forward, int targetPeer)
  {
    Projectiles.SpawnEnemyMissile(this, origin, forward, targetPeer);
    foreach (var peer in _synced)
    {
      RpcId(peer, MethodName.RpcEnemyMissile, origin, forward, targetPeer);
    }
  }

  // ------------------------------------------------------------------ helpers

  void ReplayShot(int shooter, int weaponIndex, Vector3 origin, Vector3 forward)
  {
    PlayerScript.ByPeer(shooter)?.Weapon(weaponIndex)?.SpawnProjectile(origin, forward, false);
  }

  static bool IsSaneShot(Vector3 origin, Vector3 forward) => origin.IsFinite() && forward.IsFinite() && forward.LengthSquared() > 0.5f;

  // host: recompute the roster (host plus every synced peer) and tell everyone
  void RebuildRoster()
  {
    _roster.Clear();
    _roster[1] = PlayerName;
    foreach (var peer in _synced)
    {
      _roster[peer] = _names.TryGetValue(peer, out var n) ? n : $"peer {peer}";
    }
    RosterChanged?.Invoke();

    if (Kind != SessionKind.Host)
    {
      return;
    }

    var wire = new Godot.Collections.Dictionary();
    foreach (var (peer, name) in _roster)
    {
      wire[peer] = name;
    }
    foreach (var peer in _synced)
    {
      RpcId(peer, MethodName.RpcRoster, wire);
    }
  }

  static string Sanitize(string raw)
  {
    var trimmed = raw.Trim();
    if (trimmed.Length == 0)
    {
      return "player";
    }
    return trimmed.Length > 24 ? trimmed[..24] : trimmed;
  }
}
