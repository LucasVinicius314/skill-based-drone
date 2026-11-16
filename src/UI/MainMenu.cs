using Godot;
using P2PSignaling;
using System.Collections.Generic;

// the start screen: solo, host a room, join by code or from the friends' rooms list, and the settings that
// multiplayer needs (player name, server address, friend ids). built in code, like the HUD
public partial class MainMenu : Control
{
  const string WorldScene = "res://scenes/main_scene.tscn";
  const float FriendRefreshSeconds = 4f;

  static readonly Color Green = new(0f, 1f, 0.4f, 1f);
  static readonly Color GreenDim = new(0f, 1f, 0.4f, 0.65f);

  readonly Dictionary<string, Control> _pages = new();

  NetSession _session;
  Label _status;
  LineEdit _name;
  LineEdit _code;
  LineEdit _server;
  LineEdit _friendId;
  LineEdit _friendNote;
  Label _ownId;
  Label _serverSource;
  CheckBox _listForFriends;
  VBoxContainer _friendRooms;
  string _page = "main";
  double _friendTimer;

  public override void _Ready()
  {
    Input.MouseMode = Input.MouseModeEnum.Visible;
    GetTree().Paused = false;
    _session = NetSession.Instance;

    // a dev flag (--solo, --net-host, --net-join=CODE) skips the menu
    if (_session.Pending != null)
    {
      GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, WorldScene);
      return;
    }

    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    var background = new ColorRect { Color = new Color(0.04f, 0.06f, 0.05f, 1f) };
    background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    AddChild(background);

    var center = new CenterContainer();
    center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    AddChild(center);

    var column = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
    column.AddThemeConstantOverride("separation", 14);
    center.AddChild(column);

    column.AddChild(Text("SKILL BASED DRONE", 32, Green, HorizontalAlignment.Center));

    BuildMainPage(column);
    BuildHostPage(column);
    BuildJoinPage(column);
    BuildSettingsPage(column);

    _status = Text("", 14, new Color(1f, 0.75f, 0.3f, 1f), HorizontalAlignment.Center);
    _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    column.AddChild(_status);

    _session.StatusChanged += SetStatus;
    _session.Failed += SetStatus;
    _session.FriendRoomsReceived += ShowFriendRooms;

    ShowPage("main");
    if (_session.LastError != null)
    {
      SetStatus(_session.LastError);
      _session.LastError = null;
    }
  }

  public override void _ExitTree()
  {
    if (_session == null)
    {
      return;
    }
    _session.StatusChanged -= SetStatus;
    _session.Failed -= SetStatus;
    _session.FriendRoomsReceived -= ShowFriendRooms;
  }

  public override void _Process(double delta)
  {
    if (_page != "join")
    {
      return;
    }

    _friendTimer -= delta;
    if (_friendTimer <= 0)
    {
      _friendTimer = FriendRefreshSeconds;
      _session.RefreshFriendRooms();
    }
  }

  // ------------------------------------------------------------------ pages

  void BuildMainPage(Control parent)
  {
    var page = NewPage("main", parent);

    page.AddChild(Text("Your name", 14, GreenDim, HorizontalAlignment.Left));
    _name = new LineEdit { Text = _session.PlayerName, MaxLength = 24 };
    _name.TextSubmitted += _ => ApplyName();
    _name.FocusExited += ApplyName;
    page.AddChild(_name);

    page.AddChild(Btn("SOLO", () => Start(new StartRequest(SessionKind.Solo))));
    page.AddChild(Btn("HOST A ROOM", () => ShowPage("host")));
    page.AddChild(Btn("JOIN A ROOM", () => ShowPage("join")));
    page.AddChild(Btn("SETTINGS", () => ShowPage("settings")));
    page.AddChild(Btn("QUIT", () => GetTree().Quit()));
  }

  void BuildHostPage(Control parent)
  {
    var page = NewPage("host", parent);

    var blurb = Text("Host a room and share the code it shows. Everyone flies in the same sky, against the same enemies.", 14, GreenDim, HorizontalAlignment.Center);
    blurb.AutowrapMode = TextServer.AutowrapMode.WordSmart;
    page.AddChild(blurb);
    _listForFriends = new CheckBox { Text = "Friends can see my room", ButtonPressed = true };
    page.AddChild(_listForFriends);
    page.AddChild(Btn("START HOSTING", () => Start(new StartRequest(SessionKind.Host, ListForFriends: _listForFriends.ButtonPressed))));
    page.AddChild(Btn("BACK", () => ShowPage("main")));
  }

  void BuildJoinPage(Control parent)
  {
    var page = NewPage("join", parent);

    page.AddChild(Text("Room code", 14, GreenDim, HorizontalAlignment.Left));
    _code = new LineEdit { PlaceholderText = "ABC123", MaxLength = 8 };
    _code.TextChanged += text =>
    {
      var upper = text.ToUpperInvariant();
      if (upper != text)
      {
        _code.Text = upper;
        _code.CaretColumn = upper.Length;
      }
    };
    _code.TextSubmitted += _ => Join(_code.Text);
    page.AddChild(_code);
    page.AddChild(Btn("JOIN", () => Join(_code.Text)));

    page.AddChild(Text("Friends' rooms", 14, GreenDim, HorizontalAlignment.Left));
    _friendRooms = new VBoxContainer();
    _friendRooms.AddThemeConstantOverride("separation", 6);
    page.AddChild(_friendRooms);
    page.AddChild(Btn("BACK", () => ShowPage("main")));
  }

  void BuildSettingsPage(Control parent)
  {
    var page = NewPage("settings", parent);

    page.AddChild(Text("Server address", 14, GreenDim, HorizontalAlignment.Left));
    _server = new LineEdit { PlaceholderText = "https://your-server.example" };
    page.AddChild(_server);
    _serverSource = Text("", 12, GreenDim, HorizontalAlignment.Left);
    page.AddChild(_serverSource);
    page.AddChild(Btn("SAVE SERVER", TrySaveServer));

    page.AddChild(Text("Your friend ID (give it to friends so they see your rooms)", 14, GreenDim, HorizontalAlignment.Left));
    _ownId = Text("", 14, Green, HorizontalAlignment.Left);
    page.AddChild(_ownId);
    page.AddChild(Btn("COPY MY ID", () =>
    {
      DisplayServer.ClipboardSet(P2PIdentity.OwnId);
      SetStatus("Copied.");
    }));

    page.AddChild(Text("Add a friend", 14, GreenDim, HorizontalAlignment.Left));
    _friendId = new LineEdit { PlaceholderText = "their ID" };
    _friendNote = new LineEdit { PlaceholderText = "a name to remember them by (optional)" };
    page.AddChild(_friendId);
    page.AddChild(_friendNote);
    page.AddChild(Btn("ADD FRIEND", () =>
    {
      FriendsFile.TryAdd(_friendId.Text, _friendNote.Text, out var message);
      SetStatus(message);
    }));
    page.AddChild(Btn("BACK", () => ShowPage("main")));
  }

  VBoxContainer NewPage(string id, Control parent)
  {
    var page = new VBoxContainer { Visible = false };
    page.AddThemeConstantOverride("separation", 10);
    parent.AddChild(page);
    _pages[id] = page;
    return page;
  }

  // for the dev screenshot flag
  public void ShowPageForDev(string id) => ShowPage(id);

  void ShowPage(string id)
  {
    _page = id;
    foreach (var (key, page) in _pages)
    {
      page.Visible = key == id;
    }

    if (id == "join")
    {
      _friendTimer = 0;
    }
    else if (id == "settings")
    {
      _server.Text = ServerSettings.Current ?? "";
      _serverSource.Text = ServerSettings.Source switch
      {
        ServerSource.Environment => "Set by the P2P_SIGNALING_URL environment variable.",
        ServerSource.File => $"Read from {ServerSettings.FilePath}",
        _ => "No server set yet.",
      };
      _ownId.Text = P2PIdentity.OwnId; // creates the id the first time it is needed
    }

    SetStatus("");
  }

  // ------------------------------------------------------------------ actions

  void ApplyName()
  {
    var name = _name.Text.Trim();
    if (name.Length == 0)
    {
      return;
    }
    PlayerProfile.Save(name);
    _session.PlayerName = name;
  }

  void Join(string code)
  {
    if (code.Trim().Length == 0)
    {
      SetStatus("Type the room code first.");
      return;
    }
    Start(new StartRequest(SessionKind.Client, code.Trim()));
  }

  void Start(StartRequest request)
  {
    ApplyName();
    _session.Pending = request;
    SetStatus(request.Kind == SessionKind.Solo ? "" : "Connecting...");
    GetTree().ChangeSceneToFile(WorldScene);
  }

  void TrySaveServer()
  {
    ServerSettings.TrySave(_server.Text, out var message);
    SetStatus(message);
  }

  void ShowFriendRooms(IReadOnlyList<FriendRoom> rooms)
  {
    if (_page != "join")
    {
      return;
    }

    foreach (var child in _friendRooms.GetChildren())
    {
      child.QueueFree();
    }

    if (rooms.Count == 0)
    {
      _friendRooms.AddChild(Text("None right now.", 13, GreenDim, HorizontalAlignment.Left));
      return;
    }

    foreach (var room in rooms)
    {
      var code = room.Code;
      var label = string.IsNullOrEmpty(room.FriendLabel) ? room.HostName : room.FriendLabel;
      _friendRooms.AddChild(Btn($"{label}   {room.Players}/{room.MaxPlayers}", () => Join(code)));
    }
  }

  void SetStatus(string message)
  {
    if (_status != null)
    {
      _status.Text = message;
    }
  }

  // ------------------------------------------------------------------ small widgets

  static Label Text(string text, int size, Color color, HorizontalAlignment align)
  {
    var label = new Label { Text = text, HorizontalAlignment = align };
    label.AddThemeColorOverride("font_color", color);
    label.AddThemeFontSizeOverride("font_size", size);
    return label;
  }

  static Button Btn(string text, System.Action onPressed)
  {
    var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
    button.AddThemeColorOverride("font_color", Green);
    button.AddThemeFontSizeOverride("font_size", 15);
    button.Pressed += onPressed;
    return button;
  }
}
