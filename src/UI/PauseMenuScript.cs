using Godot;
using System.Linq;
using System.Text;

public partial class PauseMenuScript : Control
{
  const string MenuScene = "res://scenes/menu.tscn";

  Label _roomLabel;
  Button _restartButton;
  double _refresh;

  public bool IsOpen { get; private set; }

  public override void _Ready()
  {
    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    ProcessMode = ProcessModeEnum.Always;
    Visible = false;
    MouseFilter = MouseFilterEnum.Ignore;

    var overlay = new ColorRect
    {
      Color = new Color(0f, 0f, 0f, 0.55f),
      MouseFilter = MouseFilterEnum.Ignore,
    };
    overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    AddChild(overlay);

    var center = new VBoxContainer();
    center.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
    center.GrowHorizontal = GrowDirection.Both;
    center.GrowVertical = GrowDirection.Both;
    center.AddThemeConstantOverride("separation", 16);
    AddChild(center);

    var title = new Label();
    title.Text = "PAUSED";
    title.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.4f, 1f));
    title.AddThemeFontSizeOverride("font_size", 28);
    title.HorizontalAlignment = HorizontalAlignment.Center;
    center.AddChild(title);

    // room code and who is in it, only when online
    _roomLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
    _roomLabel.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.4f, 0.85f));
    _roomLabel.AddThemeFontSizeOverride("font_size", 15);
    center.AddChild(_roomLabel);

    var resumeBtn = MakeButton("RESUME");
    resumeBtn.Pressed += Toggle;
    center.AddChild(resumeBtn);

    // restarting reloads the scene, which only makes sense for a solo game
    _restartButton = MakeButton("RESTART");
    _restartButton.Pressed += () => GetTree().ReloadCurrentScene();
    center.AddChild(_restartButton);

    var menuBtn = MakeButton("MAIN MENU");
    menuBtn.Pressed += LeaveToMenu;
    center.AddChild(menuBtn);

    var quitBtn = MakeButton("QUIT");
    quitBtn.Pressed += () => GetTree().Quit();
    center.AddChild(quitBtn);
  }

  static Button MakeButton(string text)
  {
    var btn = new Button();
    btn.Text = text;
    btn.CustomMinimumSize = new Vector2(180, 40);
    btn.AddThemeColorOverride("font_color", new Color(0f, 1f, 0.4f, 1f));
    btn.AddThemeFontSizeOverride("font_size", 15);
    return btn;
  }

  public override void _Input(InputEvent @event)
  {
    if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape && IsOpen)
    {
      Toggle();
      AcceptEvent();
    }
  }

  public override void _Process(double delta)
  {
    _refresh -= delta;
    if (!IsOpen || _refresh > 0)
    {
      return;
    }

    _refresh = 0.5;
    UpdateRoomLabel();
  }

  void UpdateRoomLabel()
  {
    var session = NetSession.Instance;
    _roomLabel.Visible = session.IsOnline;
    if (!session.IsOnline)
    {
      return;
    }

    var text = new StringBuilder($"ROOM {session.RoomCode ?? "..."}");
    foreach (var (peer, name) in session.Roster.OrderBy(p => p.Key != 1).ThenBy(p => p.Value))
    {
      var ping = session.PingOf(peer);
      text.Append($"\n{name}{(peer == 1 ? " (host)" : "")}{(ping > 0 ? $"  {ping} ms" : "")}");
    }
    _roomLabel.Text = text.ToString();
  }

  public void Toggle()
  {
    IsOpen = !IsOpen;
    Visible = IsOpen;
    MouseFilter = IsOpen ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;

    // online the world keeps running for everyone else, so only a solo game really pauses
    GetTree().Paused = IsOpen && !NetSession.Instance.IsOnline;
    Input.MouseMode = IsOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;

    _restartButton.Visible = !NetSession.Instance.IsOnline;
    _refresh = 0;
  }

  void LeaveToMenu()
  {
    GetTree().Paused = false;
    NetSession.Instance.Leave();
    GetTree().ChangeSceneToFile(MenuScene);
  }
}
