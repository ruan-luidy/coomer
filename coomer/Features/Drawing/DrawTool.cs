using System.Diagnostics;
using System.Numerics;
using Coomer.Features.Capture;
using Coomer.Features.Navigation;

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
  public bool AutoCircle = true;
  public bool Hide;
  public bool StampMode;
  public bool ShiftHeld;
  public int NextStampNumber = 1;

  public readonly List<Stroke> Strokes = new();
  public readonly List<Stamp> Stamps = new();

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

    if (_active.Shape is DrawShape.Free or DrawShape.Arrow)
    {
      // Arrow agora eh tambem freehand: sampling = Free. Curva suave + cabeca
      // no fim apontando pra direcao do tangente final (calculado no render).
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

  public void End()
  {
    if (_active != null
        && _active.Shape == DrawShape.Free
        && AutoCircle
        && TryDetectEllipse(_active.Points, out var c, out var axA, out var axB))
    {
      // Generalizacao do iPhone Scribble: detecta circulo OU elipse via PCA
      // dos pontos. axA = eixo maior * comprimento; axB = eixo menor (perp)
      // * comprimento. Funciona pra qualquer orientacao da elipse.
      _active.Shape = DrawShape.Circle;
      _active.Points.Clear();
      _active.Points.Add(c);
      _active.Points.Add(c + axA);
      _active.Points.Add(c + axB);
    }
    _active = null;
  }

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
    NextStampNumber = 1;
    _active = null;
  }

  public void CycleShape()
  {
    Shape = Shape switch
    {
      DrawShape.Free => DrawShape.Line,
      DrawShape.Line => DrawShape.Arrow,
      DrawShape.Arrow => DrawShape.Rect,
      DrawShape.Rect => DrawShape.Circle,
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

  // Fit de elipse pelos autovetores da covariancia (a, b = sqrt(2 lambda_i)
  // pra amostra uniforme). Aceita pelo RMS algebrico (x'/a)^2 + (y'/b)^2 - 1
  // e pela varredura angular na parametrizacao excentrica.
  private static bool TryDetectEllipse(List<Vector2> pts, out Vector2 center,
                                       out Vector2 axA, out Vector2 axB)
  {
    center = default; axA = default; axB = default;
    int n = pts.Count;
    if (n < 16) return false;

    Vector2 c = Vector2.Zero;
    foreach (var p in pts) c += p;
    c /= n;

    float mxx = 0f, myy = 0f, mxy = 0f;
    foreach (var p in pts)
    {
      var d = p - c;
      mxx += d.X * d.X; myy += d.Y * d.Y; mxy += d.X * d.Y;
    }
    mxx /= n; myy /= n; mxy /= n;

    float halfT = (mxx + myy) * 0.5f;
    float disc = MathF.Sqrt(MathF.Max(0f, halfT * halfT - (mxx * myy - mxy * mxy)));
    float l1 = halfT + disc, l2 = MathF.Max(0f, halfT - disc);
    if (l1 < 1e-6f) return false;

    Vector2 u = MathF.Abs(mxy) < 1e-4f
      ? (mxx >= myy ? new Vector2(1f, 0f) : new Vector2(0f, 1f))
      : Vector2.Normalize(new Vector2(mxy, l1 - mxx));
    var v = new Vector2(-u.Y, u.X);

    float a = MathF.Sqrt(2f * l1);
    float b = MathF.Sqrt(2f * l2);
    if (a < 12f || b < 4f || a > 8f * b) return false;

    float rss = 0f, sweep = 0f, prev = 0f;
    for (int i = 0; i < n; i++)
    {
      var d = pts[i] - c;
      float x = (d.X * u.X + d.Y * u.Y) / a;
      float y = (d.X * v.X + d.Y * v.Y) / b;
      float r = x * x + y * y - 1f;
      rss += r * r;
      float ang = MathF.Atan2(y, x);
      if (i > 0)
      {
        float diff = ang - prev;
        if (diff > MathF.PI) diff -= MathF.Tau;
        else if (diff < -MathF.PI) diff += MathF.Tau;
        sweep += diff;
      }
      prev = ang;
    }
    if (MathF.Sqrt(rss / n) > 0.35f) return false;
    if (MathF.Abs(sweep) < MathF.PI * 1.5f) return false;

    center = c;
    axA = u * a;
    axB = v * b;
    return true;
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
