using Godot;

public partial class PauseMenuScript : Control
{
  bool _visible = false;

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

    var resumeBtn = MakeButton("RESUME");
    resumeBtn.Pressed += Toggle;
    center.AddChild(resumeBtn);

    var restartBtn = MakeButton("RESTART");
    restartBtn.Pressed += () => GetTree().ReloadCurrentScene();
    center.AddChild(restartBtn);

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
    if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape && _visible)
    {
      Toggle();
      AcceptEvent();
    }
  }

  public void Toggle()
  {
    _visible = !_visible;
    Visible = _visible;
    MouseFilter = _visible ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    GetTree().Paused = _visible;
    Input.MouseMode = _visible ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
  }
}
