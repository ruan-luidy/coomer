using System.Diagnostics;
using System.Numerics;
using Coomer.Features.Capture;
using Coomer.Features.Navigation;
using Coomer.Features.Stickers;

namespace Coomer.Features.Drawing;

public sealed class DrawTool
{
  private static readonly Vector4[] Pallet =
  {
    new(1.00f, 0.20f, 0.20f, 1f),
    new(1.00f, 0.85f, 0.20f, 1f),
    new(0.25f, 1.00f, 0.40f, 1f),
    new(0.30f, 0.70f, 1.00f, 1f),
    new(1.00f, 0.40f, 1.00f, 1f),
    new(1.00f, 1.00f, 1.00f, 1f),
    new(0.00f, 0.00f, 0.00f, 1f),
  };

  public bool IsEnabled;
  public DrawShape Shape = DrawShape.Free;
  public int ColorIndex;
  public float Thickness = 4f;
  public bool Hide;
  public bool StampMode;
  public bool StickerMode;
  public bool StickerMirror;
  public bool TextMode;
  public bool ShiftHeld;
  public int NextStampNumber = 1;
  public float StickerSize = 128f; // diametro em pixel de imagem
  public int SelectedStickerIndex = -1;
  public Vector2 DragOffset;
  public bool DraggingSticker;

  public readonly List<Stroke> Strokes = new();
  public readonly List<Stamp> Stamps = new();
  public readonly List<StickerStamp> StickerStamps = new();
  public readonly List<TextStamp> TextStamps = new();
  public int TextFontSize = 32;
  public TextStamp? ActiveText;

  private Stroke? _active;
  private readonly OneEuroFilterV2 _filter = new();
  private readonly Stopwatch _watch = new();

  public Vector4 CurrentColor => Pallet[ColorIndex];

  public void Begin(Vector2 cursorScreen, Vector2 windowSize, Screenshot shot,
                    Camera camera, bool mirror)
  {
    _filter.Reset();
    _watch.Restart();
    var smoothed = _filter.Step(cursorScreen, 1F / 120f);
    var p = ScreenToImage(smoothed, windowSize, shot, camera, mirror);
    _active = new Stroke()
    {
      Shape = Shape,
      Color = CurrentColor,
      Thickness = Thickness,
    };
    _active.Points.Add(p);
    Strokes.Add(_active);
  }

  public void Move(Vector2 cursorScreen, Vector2 windowSize, Screenshot shot,
                   Camera camera, bool mirror)
  {
    if (_active == null) return;

    float dt = (float)_watch.Elapsed.TotalSeconds;
    _watch.Restart();
    if (dt <= 0f) dt = 1f / 120f;

    var smoothed = _filter.Step(cursorScreen, dt);
    var p = ScreenToImage(smoothed, windowSize, shot, camera, mirror);

    if (_active.Shape is DrawShape.Free or DrawShape.Arrow or DrawShape.Highlighter)
    {
      // Highlighter eh freehand grosso e translucido (alpha 0.4 no render).
      if (Vector2.DistanceSquared(_active.Points[^1], p) < 0.25f) return;
      _active.Points.Add(p);
    }
    else
    {
      if (ShiftHeld) p = ApplyShiftConstraint(_active.Shape, _active.Points[0], p);
      if (_active.Points.Count < 2) _active.Points.Add(p);
      else _active.Points[1] = p;
    }
  }

  public void End() => _active = null;

  /// <summary>Solta um stamp numerado no cursor (em modo StampMode).</summary>
  public void DropStamp(Vector2 cursorScreen, Vector2 windowSize, Screenshot shot,
                        Camera camera, bool mirror)
  {
    var p = ScreenToImage(cursorScreen, windowSize, shot, camera, mirror);
    Stamps.Add(new Stamp
    {
      Center = p,
      Number = NextStampNumber++,
      Color = CurrentColor,
      // Raio em pixel de imagem: cresce com thickness pra ficar visivel mesmo
      // com brush fino. ~5x a meia-espessura da uma bolinha bem proporcional.
      Radius = MathF.Max(12f, Thickness * 2.5f),
    });
  }

  public void Undo()
  {
    // desfaz a coisa mais recente: se o ultimo stamp e mais novo que o ultimo
    // traco, desfaz stamp; senao desfaz traco. Como nao temos timestamp,
    // priorizamos remover de quem foi adicionado por ultimo — heuristica:
    // se ha stamp e stampMode, desfaz stamp; senao traco. Pratico o bastante.
    if (TextMode && TextStamps.Count > 0) { TextStamps.RemoveAt(TextStamps.Count - 1); return; }
    if (StickerMode && StickerStamps.Count > 0) { StickerStamps.RemoveAt(StickerStamps.Count - 1); return; }
    if (StampMode && Stamps.Count > 0)
    {
      Stamps.RemoveAt(Stamps.Count - 1);
      if (NextStampNumber > 1) NextStampNumber--;
      return;
    }
    if (Strokes.Count > 0)
    {
      Strokes.RemoveAt(Strokes.Count - 1);
      _active = null;
      return;
    }
    if (StickerStamps.Count > 0) { StickerStamps.RemoveAt(StickerStamps.Count - 1); return; }
    if (Stamps.Count > 0)
    {
      Stamps.RemoveAt(Stamps.Count - 1);
      if (NextStampNumber > 1) NextStampNumber--;
    }
  }

  public void Clear()
  {
    Strokes.Clear();
    Stamps.Clear();
    StickerStamps.Clear();
    TextStamps.Clear();
    ActiveText = null;
    NextStampNumber = 1;
    _active = null;
    SelectedStickerIndex = -1;
    DraggingSticker = false;
  }

  public int HitTestSticker(Vector2 cursorImg, StickerCache cache)
  {
    for (int i = StickerStamps.Count - 1; i >= 0; i--)
    {
      var s = StickerStamps[i];
      var entry = cache.All.FirstOrDefault(e => e.Path == s.Path);
      if (entry == null) continue;
      float aspect = entry.Height == 0 ? 1f : (float)entry.Height / entry.Width;
      float halfW = s.HalfSize;
      float halfH = s.HalfSize * aspect;
      var local = cursorImg - s.Center;
      float cos = MathF.Cos(-s.Rotation);
      float sin = MathF.Sin(-s.Rotation);
      var rot = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
      if (MathF.Abs(rot.X) <= halfW && MathF.Abs(rot.Y) <= halfH) return i;
    }
    return -1;
  }

  public void SelectSticker(int idx)
  {
    SelectedStickerIndex = (idx >= 0 && idx < StickerStamps.Count) ? idx : -1;
    DraggingSticker = false;
  }

  public void DeselectSticker()
  {
    SelectedStickerIndex = -1;
    DraggingSticker = false;
  }

  public void BeginStickerDrag(Vector2 cursorImg)
  {
    if (SelectedStickerIndex < 0) return;
    DragOffset = StickerStamps[SelectedStickerIndex].Center - cursorImg;
    DraggingSticker = true;
  }

  public void DragSticker(Vector2 cursorImg)
  {
    if (!DraggingSticker || SelectedStickerIndex < 0) return;
    StickerStamps[SelectedStickerIndex].Center = cursorImg + DragOffset;
  }

  public void EndStickerDrag() => DraggingSticker = false;

  public void RotateSelected(float deltaRadians)
  {
    if (SelectedStickerIndex < 0) return;
    StickerStamps[SelectedStickerIndex].Rotation += deltaRadians;
  }

  public void ResizeSelected(float delta)
  {
    if (SelectedStickerIndex < 0) return;
    var s = StickerStamps[SelectedStickerIndex];
    s.HalfSize = Math.Clamp(s.HalfSize + delta, 12f, 1024f);
  }

  public void DeleteSelected()
  {
    if (SelectedStickerIndex < 0 || SelectedStickerIndex >= StickerStamps.Count) return;
    StickerStamps.RemoveAt(SelectedStickerIndex);
    SelectedStickerIndex = -1;
    DraggingSticker = false;
  }

  public void BeginText(Vector2 cursorScreen, Vector2 windowSize, Screenshot shot, Camera camera, bool mirror)
  {
    CommitActiveText();
    var p = ScreenToImage(cursorScreen, windowSize, shot, camera, mirror);
    ActiveText = new TextStamp { TopLeft = p, Color = CurrentColor, FontSizePx = TextFontSize };
  }

  public void TypeChar(char c)
  {
    if (ActiveText == null) return;
    if (c == '\b')
    {
      if (ActiveText.Text.Length > 0) ActiveText.Text = ActiveText.Text[..^1];
      return;
    }
    if (c < ' ') return;
    ActiveText.Text += c;
  }

  public void CommitActiveText()
  {
    if (ActiveText == null) return;
    if (!string.IsNullOrEmpty(ActiveText.Text)) TextStamps.Add(ActiveText);
    ActiveText = null;
  }

  public void CancelActiveText() => ActiveText = null;

  public void TextFontSizeDelta(int d) => TextFontSize = Math.Clamp(TextFontSize + d, 10, 256);

  public void DropSticker(Vector2 cursorScreen, Vector2 windowSize, Screenshot shot,
                          Camera camera, bool mirror, string stickerPath)
  {
    var p = ScreenToImage(cursorScreen, windowSize, shot, camera, mirror);
    StickerStamps.Add(new StickerStamp { Center = p, Path = stickerPath, HalfSize = StickerSize * 0.5f, MirrorH = StickerMirror });
  }

  public void StickerSizeDelta(float d)
    => StickerSize = Math.Clamp(StickerSize + d, 24f, 1024f);

  public void CycleShape()
  {
    Shape = Shape switch
    {
      DrawShape.Free => DrawShape.Line,
      DrawShape.Line => DrawShape.Arrow,
      DrawShape.Arrow => DrawShape.Rect,
      DrawShape.Rect => DrawShape.Circle,
      DrawShape.Circle => DrawShape.Highlighter,
      _ => DrawShape.Free,
    };
  }

  public void CycleColor() => ColorIndex = (ColorIndex + 1) % Pallet.Length;

  public void ThicknessDelta(float d)
    => Thickness = Math.Clamp(Thickness + d, 1f, 80f);

  // Snap pra Line em 45°; pra Rect e Circle, bbox quadrado (= retangulo quadrado
  // ou circulo perfeito). Arrow agora eh freehand, nao tem 2-point pra snapar.
  private static Vector2 ApplyShiftConstraint(DrawShape shape, Vector2 start, Vector2 end)
  {
    var d = end - start;
    if (shape is DrawShape.Line)
    {
      float ang = MathF.Atan2(d.Y, d.X);
      float step = MathF.PI / 4f;
      float snap = MathF.Round(ang / step) * step;
      float len = d.Length();
      return start + new Vector2(MathF.Cos(snap), MathF.Sin(snap)) * len;
    }
    if (shape is DrawShape.Rect or DrawShape.Circle)
    {
      float side = MathF.Max(MathF.Abs(d.X), MathF.Abs(d.Y));
      float sx = d.X >= 0f ? 1f : -1f;
      float sy = d.Y >= 0f ? 1f : -1f;
      return start + new Vector2(sx * side, sy * side);
    }
    return end;
  }

  private static Vector2 ScreenToImage(Vector2 cursor, Vector2 windowSize,
                                       Screenshot shot, Camera camera, bool mirror)
  {
    var centered = cursor - windowSize * 0.5f;
    var world = centered / camera.Scale;
    var p = world + camera.Position + new Vector2(shot.Width, shot.Height) * 0.5f;
    if (mirror) p.X = shot.Width - p.X;
    return p;
  }
}
