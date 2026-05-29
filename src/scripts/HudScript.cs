using Godot;

public partial class HudScript : Control
{
  // set each frame by PlayerScript
  public float SpeedTotal;
  public float SpeedVertical;
  public float Altitude;
  public float HeadingDeg;   // 0-360
  public float PitchDeg;     // nose-up positive
  public float BankDeg;      // right-roll positive

  // colours
  const string FontColor = "font_color";

  static readonly Color ColMain   = new Color(0.0f, 1.0f, 0.4f, 0.85f);
  static readonly Color ColDim    = new Color(0.0f, 1.0f, 0.4f, 0.35f);
  static readonly Color ColText   = new Color(0.0f, 1.0f, 0.4f, 1.0f);

  // pitch ladder config
  const float LadderDegSpacing = 10f;   // degrees between rungs
  const float LadderPixPerDeg  = 8f;    // pixels per degree on screen
  const float LadderHalfWidth  = 60f;
  const float LadderTickGap    = 10f;   // gap in the centre of each rung
  const int   LadderRange      = 9;     // rungs above+below horizon (±90°)

  // compass tape config
  const float CompassWidth     = 320f;
  const float CompassHeight    = 28f;
  const float CompassPixPerDeg = CompassWidth / 60f; // 60° visible window

  public WeaponScript[] Weapons;

  Label _speedLabel;
  Label _altLabel;
  Label _hdgLabel;
  Label _bankLabel;
  Label _vsLabel;
  Label _fpsLabel;
  Label _timerLabel;
  double _elapsed;

  const int   AmmoMaxIcons  = 10;
  const float AmmoIconSize  = 10f;
  const float AmmoIconGap   = 4f;
  const float AmmoBarH      = 5f;
  const float AmmoLabelH    = 16f; // space for weapon name + count on same row
  const float AmmoIconsY    = AmmoLabelH + 4f;    // icons start below label row
  const float AmmoBarY      = AmmoIconsY + AmmoIconSize + 8f; // bar starts below icons with gap
  const float AmmoWidgetW   = AmmoMaxIcons * (AmmoIconSize + AmmoIconGap) - AmmoIconGap;
  const float AmmoWidgetH   = AmmoBarY + AmmoBarH + 4f;
  const float AmmoWidgetGap = 12f;

  static readonly string[] WeaponNames = { "GUN", "MSL" };

  public override void _Ready()
  {
    // fill parent
    SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

    // --- top-left panel: FPS + timer ---
    var topPanel = new PanelContainer();
    topPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
    topPanel.Position = new Vector2(16, 16);
    var topStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
    topPanel.AddThemeStyleboxOverride("panel", topStyle);
    AddChild(topPanel);

    var topVbox = new VBoxContainer();
    topVbox.AddThemeConstantOverride("separation", 2);
    topPanel.AddChild(topVbox);

    _fpsLabel = MakeLabel("FPS  ---");
    _timerLabel = MakeLabel("TIME 00:00");
    topVbox.AddChild(_fpsLabel);
    topVbox.AddChild(_timerLabel);

    // --- text readout panel (bottom-left) ---
    var panel = new PanelContainer();
    panel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
    panel.Position = new Vector2(16, -160);
    var style = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
    panel.AddThemeStyleboxOverride("panel", style);
    AddChild(panel);

    var vbox = new VBoxContainer();
    vbox.AddThemeConstantOverride("separation", 2);
    panel.AddChild(vbox);

    _speedLabel = MakeLabel("SPD  ---");
    _vsLabel    = MakeLabel("V/S  ---");
    _altLabel   = MakeLabel("ALT  ---");
    _hdgLabel   = MakeLabel("HDG  ---");
    _bankLabel  = MakeLabel("BANK ---");

    vbox.AddChild(_speedLabel);
    vbox.AddChild(_vsLabel);
    vbox.AddChild(_altLabel);
    vbox.AddChild(_hdgLabel);
    vbox.AddChild(_bankLabel);

  }

  static Label MakeLabel(string text)
  {
    var lbl = new Label();
    lbl.Text = text;
    lbl.AddThemeColorOverride(FontColor, ColText);
    lbl.AddThemeFontSizeOverride("font_size", 13);
    return lbl;
  }

  public override void _Process(double delta)
  {
    _elapsed += delta;
    var minutes = (int)(_elapsed / 60);
    var seconds = (int)(_elapsed % 60);
    _fpsLabel.Text = $"FPS  {Engine.GetFramesPerSecond()}";
    _timerLabel.Text = $"TIME {minutes:D2}:{seconds:D2}";

    // update text labels
    _speedLabel.Text = $"SPD  {SpeedTotal:F0} m/s";
    _vsLabel.Text    = $"V/S  {(SpeedVertical >= 0 ? "+" : "")}{SpeedVertical:F1} m/s";
    _altLabel.Text   = $"ALT  {Altitude:F0} m";
    _hdgLabel.Text   = $"HDG  {HeadingDeg:F0}°";
    _bankLabel.Text  = $"BANK {(BankDeg >= 0 ? "+" : "")}{BankDeg:F0}°";

    QueueRedraw();
  }

  public override void _Draw()
  {
    var center = Size * 0.5f;
    DrawPitchLadder(center);
    DrawCompassTape();
    DrawBankIndicator(center);
    DrawAmmoWidgets();
  }

  void DrawAmmoWidgets()
  {
    if (Weapons == null) return;

    // stack widgets bottom-right, growing upward
    var totalH = Weapons.Length * AmmoWidgetH + (Weapons.Length - 1) * AmmoWidgetGap;
    var baseX = Size.X - AmmoWidgetW - 20f;
    var baseY = Size.Y - 20f - totalH;

    for (var i = 0; i < Weapons.Length; i++)
    {
      var origin = new Vector2(baseX, baseY + i * (AmmoWidgetH + AmmoWidgetGap));
      DrawAmmoWidget(Weapons[i], i, origin);
    }
  }

  void DrawAmmoWidget(WeaponScript w, int index, Vector2 origin)
  {
    var tag = index < WeaponNames.Length ? WeaponNames[index] : $"W{index}";
    var labelColor = w.IsReloading ? new Color(1f, 0.7f, 0f, 1f) : ColText;
    var widgetW = AmmoWidgetW + 16f;

    DrawRect(new Rect2(origin, new Vector2(widgetW, AmmoWidgetH)),
      new Color(0f, 0f, 0f, 0.55f));

    // weapon name — left side of header row
    DrawString(ThemeDB.FallbackFont, origin + new Vector2(4f, AmmoLabelH - 2f),
      tag, HorizontalAlignment.Left, -1, 13, labelColor);

    // ammo count — right side of header row
    string countStr;
    if (w.MaxAmmo <= 0)
    {
      countStr = "∞";
    }
    else if (w.IsReloading)
    {
      countStr = "RLDG";
    }
    else
    {
      countStr = $"{w.Ammo}/{w.MaxAmmo}";
    }
    DrawString(ThemeDB.FallbackFont, origin + new Vector2(40f, AmmoLabelH - 2f),
      countStr, HorizontalAlignment.Left, -1, 13, labelColor);

    if (w.MaxAmmo > 0)
    {
      var iconsOrigin = origin + new Vector2(4f, AmmoIconsY);
      DrawAmmoIcons(w, index == 1, iconsOrigin);
    }

    DrawAmmoBar(w, origin);
  }

  void DrawAmmoIcons(WeaponScript w, bool isMissile, Vector2 iconsOrigin)
  {
    var icons = Mathf.Min(w.MaxAmmo, AmmoMaxIcons);
    var ammoPerIcon = w.MaxAmmo / (float)icons;
    var filledIcons = Mathf.CeilToInt(w.Ammo / ammoPerIcon);
    var lowAmmo = filledIcons <= icons / 4;

    for (var j = 0; j < icons; j++)
    {
      var pos = new Vector2(iconsOrigin.X + j * (AmmoIconSize + AmmoIconGap), iconsOrigin.Y);
      var filled = j < filledIcons;
      Color iconColor;
      if (!filled)
      {
        iconColor = new Color(ColMain.R, ColMain.G, ColMain.B, 0.18f);
      }
      else if (lowAmmo)
      {
        iconColor = new Color(1f, 0.2f, 0.2f, 1f);
      }
      else
      {
        iconColor = ColMain;
      }

      if (isMissile)
      {
        DrawMissileIcon(pos, iconColor, filled);
      }
      else
      {
        DrawBulletIcon(pos, iconColor, filled);
      }
    }
  }

  void DrawAmmoBar(WeaponScript w, Vector2 origin)
  {
    var barOrigin = origin + new Vector2(4f, AmmoBarY);
    var barW = AmmoWidgetW + 8f;

    DrawRect(new Rect2(barOrigin, new Vector2(barW, AmmoBarH)),
      new Color(ColMain.R, ColMain.G, ColMain.B, 0.15f));

    float fillRatio;
    Color fillColor;
    if (w.IsReloading)
    {
      fillRatio = w.ReloadProgress;
      fillColor = new Color(1f, 0.7f, 0f, 0.9f);
    }
    else if (w.MaxAmmo > 0)
    {
      fillRatio = (float)w.Ammo / w.MaxAmmo;
      fillColor = ColMain;
    }
    else
    {
      fillRatio = 1f;
      fillColor = ColMain;
    }

    var fillW = barW * fillRatio;
    if (fillW > 0f)
    {
      DrawRect(new Rect2(barOrigin, new Vector2(fillW, AmmoBarH)), fillColor);
    }

    DrawRect(new Rect2(barOrigin, new Vector2(barW, AmmoBarH)),
      new Color(ColMain.R, ColMain.G, ColMain.B, 0.4f), false, 1f);
  }

  void DrawBulletIcon(Vector2 pos, Color color, bool filled)
  {
    // bullet: rounded rectangle — tall and narrow
    var rect = new Rect2(pos.X + 2f, pos.Y, AmmoIconSize - 4f, AmmoIconSize);
    if (filled)
    {
      DrawRect(rect, color);
    }
    else
    {
      DrawRect(rect, color, false, 1f);
    }
    // casing tip (top cap)
    DrawRect(new Rect2(pos.X + 2f, pos.Y, AmmoIconSize - 4f, 3f), color);
  }

  void DrawMissileIcon(Vector2 pos, Color color, bool filled)
  {
    // missile: triangle nose + rectangle body
    var bodyTop = pos.Y + 4f;
    var bodyRect = new Rect2(pos.X + 2f, bodyTop, AmmoIconSize - 4f, AmmoIconSize - 4f);
    // nose triangle
    var noseL = new Vector2(pos.X + 2f, bodyTop);
    var noseR = new Vector2(pos.X + AmmoIconSize - 2f, bodyTop);
    var noseTip = new Vector2(pos.X + AmmoIconSize * 0.5f, pos.Y);
    if (filled)
    {
      DrawColoredPolygon(new[] { noseTip, noseL, noseR }, color);
      DrawRect(bodyRect, color);
    }
    else
    {
      DrawLine(noseTip, noseL, color, 1f);
      DrawLine(noseTip, noseR, color, 1f);
      DrawRect(bodyRect, color, false, 1f);
    }
  }

  void DrawPitchLadder(Vector2 center)
  {
    float bankRad  = Mathf.DegToRad(BankDeg);
    float cos      = Mathf.Cos(bankRad);
    float sin      = Mathf.Sin(bankRad);

    // clip drawing to a vertical band around centre
    for (int i = -LadderRange; i <= LadderRange; i++)
    {
      float deg = i * LadderDegSpacing;
      float yOffset = -(deg - PitchDeg) * LadderPixPerDeg;
      var rungCenter = new Vector2(0, yOffset);
      var rungRotated = new Vector2(
        rungCenter.X * cos - rungCenter.Y * sin,
        rungCenter.X * sin + rungCenter.Y * cos
      );
      var worldCenter = center + rungRotated;
      var rungDir = new Vector2(cos, sin);

      if (Mathf.IsZeroApprox(deg))
      {
        DrawLine(worldCenter - rungDir * (LadderHalfWidth * 1.5f), worldCenter + rungDir * (LadderHalfWidth * 1.5f), ColMain, 2f);
        continue;
      }

      // half-width direction (along the rung, bank-rotated)
      var rungPerp = new Vector2(-sin, cos); // perpendicular = up direction rotated

      bool aboveHorizon = deg > 0;
      var lineColor = aboveHorizon ? ColMain : ColDim;
      float hw = LadderHalfWidth;

      // left tick
      var lStart = worldCenter - rungDir * hw;
      var lEnd   = worldCenter - rungDir * LadderTickGap;
      // right tick
      var rStart = worldCenter + rungDir * LadderTickGap;
      var rEnd   = worldCenter + rungDir * hw;

      DrawLine(lStart, lEnd, lineColor, 1.5f);
      DrawLine(rStart, rEnd, lineColor, 1.5f);

      // small vertical serifs at outer ends
      var serifLen = 6f * (aboveHorizon ? 1f : -1f);
      DrawLine(lStart, lStart + rungPerp * serifLen, lineColor, 1.5f);
      DrawLine(rEnd,   rEnd   + rungPerp * serifLen, lineColor, 1.5f);

      // degree label on the right side
      var labelPos = rEnd + rungDir * 4f;
      DrawString(ThemeDB.FallbackFont, labelPos, $"{Mathf.Abs((int)deg)}",
        HorizontalAlignment.Left, -1, 10, ColText);
    }

    // centre reticle — two short horizontal bars with a gap
    var reticleY = center;
    DrawLine(reticleY + new Vector2(-40, 0), reticleY + new Vector2(-12, 0), ColMain, 2f);
    DrawLine(reticleY + new Vector2( 12, 0), reticleY + new Vector2( 40, 0), ColMain, 2f);
    DrawLine(reticleY + new Vector2(-4, 0),  reticleY + new Vector2( 4, 0),  ColMain, 1f);
  }

  void DrawCompassTape(  )
  {
    var center = Size * 0.5f;
    float tapeLeft = center.X - CompassWidth * 0.5f;
    float tapeTop  = 12f;

    // background
    DrawRect(new Rect2(tapeLeft, tapeTop, CompassWidth, CompassHeight),
      new Color(0, 0, 0, 0.5f));

    // tick marks every 5°, labels every 30°
    // snap to the nearest 5° grid so ticks slide continuously with heading
    float headingFrac = HeadingDeg % 5f;  // sub-step fractional offset
    for (int step = -13; step <= 13; step++)
    {
      float degOffset = step * 5f - headingFrac;
      int absDegInt = ((int)Mathf.Round(HeadingDeg + degOffset) % 360 + 360) % 360;
      float x = center.X + degOffset * CompassPixPerDeg;

      bool isMajor = absDegInt % 30 == 0;
      float tickH = isMajor ? CompassHeight * 0.6f : CompassHeight * 0.3f;
      DrawLine(new Vector2(x, tapeTop + CompassHeight - tickH),
               new Vector2(x, tapeTop + CompassHeight), ColMain, 1.5f);

      if (isMajor)
      {
        var label = absDegInt switch
        {
          0   => "N",
          90  => "E",
          180 => "S",
          270 => "W",
          _   => $"{absDegInt}"
        };
        DrawString(ThemeDB.FallbackFont,
          new Vector2(x, tapeTop + 13),
          label, HorizontalAlignment.Center, -1, 11, ColText);
      }
    }

    // centre lubber line
    DrawLine(new Vector2(center.X, tapeTop),
             new Vector2(center.X, tapeTop + CompassHeight), ColMain, 2f);

    // border
    DrawRect(new Rect2(tapeLeft, tapeTop, CompassWidth, CompassHeight),
      ColDim, false, 1f);
  }

  void DrawBankIndicator(Vector2 center)
  {
    // arc at bottom of screen with a moving pointer
    float radius   = 90f;
    float arcY     = center.Y + 160f;
    var   arcCenter = new Vector2(center.X, arcY);

    // draw arc from -60° to +60° (in screen space, 0° = top)
    int steps = 48;
    for (int i = 0; i < steps; i++)
    {
      float a0 = Mathf.DegToRad(-60f + i * (120f / steps)) - Mathf.Pi * 0.5f;
      float a1 = Mathf.DegToRad(-60f + (i + 1) * (120f / steps)) - Mathf.Pi * 0.5f;
      DrawLine(
        arcCenter + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius,
        arcCenter + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius,
        ColDim, 1.5f);
    }

    // tick marks at 0, ±10, ±20, ±30, ±45, ±60
    foreach (var tickDeg in new[] { -60, -45, -30, -20, -10, 0, 10, 20, 30, 45, 60 })
    {
      float a = Mathf.DegToRad(tickDeg) - Mathf.Pi * 0.5f;
      bool major = tickDeg % 30 == 0 || tickDeg == 0;
      float inner = major ? radius - 10f : radius - 6f;
      DrawLine(
        arcCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner,
        arcCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
        ColMain, major ? 2f : 1.5f);
    }

    // bank pointer triangle (points inward toward arc centre)
    float bankRad = Mathf.DegToRad(BankDeg) - Mathf.Pi * 0.5f;
    var tip  = arcCenter + new Vector2(Mathf.Cos(bankRad), Mathf.Sin(bankRad)) * (radius - 14f);
    var base1 = arcCenter + new Vector2(Mathf.Cos(bankRad - 0.08f), Mathf.Sin(bankRad - 0.08f)) * (radius + 2f);
    var base2 = arcCenter + new Vector2(Mathf.Cos(bankRad + 0.08f), Mathf.Sin(bankRad + 0.08f)) * (radius + 2f);
    DrawColoredPolygon(new Vector2[] { tip, base1, base2 }, ColMain);
  }
}
