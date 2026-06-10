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

  // Direct least-squares ellipse fit (Halir & Flusser 1998, versao estavel do
  // Fitzgibbon-Pilu-Fisher 1999): minimiza ||D a||^2 com a^T C a = 1, onde a
  // constraint forca 4ac-b^2 > 0 (elipse). Reduz pra autovalor 3x3 e funciona
  // mesmo com cobertura parcial. Conic geral -> parametros geometricos via
  // diagonalizacao da forma quadratica [[A, B/2],[B/2, C]].
  private static bool TryDetectEllipse(List<Vector2> pts, out Vector2 center,
                                       out Vector2 axA, out Vector2 axB)
  {
    center = default; axA = default; axB = default;
    int n = pts.Count;
    if (n < 12) return false;

    // Normalizacao pra estabilidade numerica.
    double mx = 0, my = 0;
    foreach (var p in pts) { mx += p.X; my += p.Y; }
    mx /= n; my /= n;
    double sx2 = 0, sy2 = 0;
    foreach (var p in pts)
    {
      sx2 += (p.X - mx) * (p.X - mx);
      sy2 += (p.Y - my) * (p.Y - my);
    }
    double scale = Math.Sqrt(Math.Max(sx2, sy2) / n);
    if (scale < 1e-3) return false;
    double invS = 1.0 / scale;

    // S1 = D1^T D1, S2 = D1^T D2, S3 = D2^T D2 onde D1=[x^2, xy, y^2], D2=[x,y,1].
    Span<double> S1 = stackalloc double[9];
    Span<double> S2 = stackalloc double[9];
    Span<double> S3 = stackalloc double[9];
    foreach (var p in pts)
    {
      double x = (p.X - mx) * invS;
      double y = (p.Y - my) * invS;
      double x2 = x * x, xy = x * y, y2 = y * y;
      S1[0] += x2 * x2; S1[1] += x2 * xy; S1[2] += x2 * y2;
                        S1[4] += xy * xy; S1[5] += xy * y2;
                                          S1[8] += y2 * y2;
      S2[0] += x2 * x;  S2[1] += x2 * y;  S2[2] += x2;
      S2[3] += xy * x;  S2[4] += xy * y;  S2[5] += xy;
      S2[6] += y2 * x;  S2[7] += y2 * y;  S2[8] += y2;
      S3[0] += x * x;   S3[1] += x * y;   S3[2] += x;
                        S3[4] += y * y;   S3[5] += y;
                                          S3[8] += 1;
    }
    S1[3] = S1[1]; S1[6] = S1[2]; S1[7] = S1[5];
    S3[3] = S3[1]; S3[6] = S3[2]; S3[7] = S3[5];

    Span<double> S3inv = stackalloc double[9];
    if (!Inverse3x3(S3, S3inv)) return false;

    // T = -S3^-1 * S2^T
    Span<double> T = stackalloc double[9];
    for (int i = 0; i < 3; i++)
      for (int j = 0; j < 3; j++)
      {
        double s = 0;
        for (int k = 0; k < 3; k++) s += S3inv[i * 3 + k] * S2[j * 3 + k];
        T[i * 3 + j] = -s;
      }

    // M = C1^-1 * (S1 + S2 T). C1^-1 troca a primeira/terceira linha e divide por 2.
    Span<double> M = stackalloc double[9];
    for (int j = 0; j < 3; j++)
    {
      double r0 = S1[j], r1 = S1[3 + j], r2 = S1[6 + j];
      for (int k = 0; k < 3; k++)
      {
        double tkj = T[k * 3 + j];
        r0 += S2[k] * tkj;
        r1 += S2[3 + k] * tkj;
        r2 += S2[6 + k] * tkj;
      }
      M[j] = 0.5 * r2;
      M[3 + j] = -r1;
      M[6 + j] = 0.5 * r0;
    }

    // Autovalores de M via polinomio caracteristico (cubica).
    double trace = M[0] + M[4] + M[8];
    double c2 = M[0] * M[4] - M[1] * M[3]
              + M[0] * M[8] - M[2] * M[6]
              + M[4] * M[8] - M[5] * M[7];
    double detM = M[0] * (M[4] * M[8] - M[5] * M[7])
                - M[1] * (M[3] * M[8] - M[5] * M[6])
                + M[2] * (M[3] * M[7] - M[4] * M[6]);
    Span<double> lambdas = stackalloc double[3];
    int nReal = SolveCubic(-trace, c2, -detM, lambdas);

    Span<double> abc = stackalloc double[3];
    bool found = false;
    for (int i = 0; i < nReal; i++)
    {
      if (!Eigenvector3x3(M, lambdas[i], abc)) continue;
      double cond = 4 * abc[0] * abc[2] - abc[1] * abc[1];
      if (cond > 0)
      {
        found = true;
        break;
      }
    }
    if (!found) return false;

    double A = abc[0], B = abc[1], C = abc[2];
    double D = T[0] * A + T[1] * B + T[2] * C;
    double E = T[3] * A + T[4] * B + T[5] * C;
    double F = T[6] * A + T[7] * B + T[8] * C;

    // Conic geral -> centro: resolve [2A B; B 2C][cx;cy] = -[D;E].
    double detQ = 4 * A * C - B * B;
    if (detQ <= 1e-12) return false;
    double cx = (B * E - 2 * C * D) / detQ;
    double cy = (B * D - 2 * A * E) / detQ;
    double Fc = A * cx * cx + B * cx * cy + C * cy * cy + D * cx + E * cy + F;

    // Eigenvalues de Q = [[A, B/2],[B/2, C]] dao 1/a^2 e 1/b^2 (a menos do Fc).
    double tQ = A + C;
    double dQ = A * C - B * B / 4;
    double sq = Math.Sqrt(Math.Max(0, tQ * tQ / 4 - dQ));
    double l1 = tQ / 2 - sq; // menor autovalor -> eixo MAIOR
    double l2 = tQ / 2 + sq; // maior autovalor -> eixo MENOR
    if (l1 < 1e-12 || l2 < 1e-12) return false;
    double semiMajorSq = -Fc / l1;
    double semiMinorSq = -Fc / l2;
    if (semiMajorSq <= 0 || semiMinorSq <= 0) return false;
    double semiMajor = Math.Sqrt(semiMajorSq);
    double semiMinor = Math.Sqrt(semiMinorSq);

    // Direcao do eixo MAIOR: autovetor de Q pro autovalor l1.
    double ux, uy;
    if (Math.Abs(B) > 1e-9)
    {
      ux = -B / 2; uy = A - l1;
      double m = Math.Sqrt(ux * ux + uy * uy);
      ux /= m; uy /= m;
    }
    else
    {
      if (A <= C) { ux = 1; uy = 0; } else { ux = 0; uy = 1; }
    }

    // De volta pra escala/origem.
    float majorImg = (float)(semiMajor * scale);
    float minorImg = (float)(semiMinor * scale);
    if (majorImg < 12f || minorImg < 4f || majorImg > 10f * minorImg) return false;

    // RMS algebrico no espaco normalizado pra sanity-check.
    double rss = 0;
    foreach (var p in pts)
    {
      double x = (p.X - mx) * invS;
      double y = (p.Y - my) * invS;
      double r = A * x * x + B * x * y + C * y * y + D * x + E * y + F;
      rss += r * r;
    }
    double normFactor = Math.Max(Math.Abs(Fc), 1e-6);
    if (Math.Sqrt(rss / n) / normFactor > 0.5) return false;

    center = new Vector2((float)(cx * scale + mx), (float)(cy * scale + my));
    axA = new Vector2((float)(ux * majorImg), (float)(uy * majorImg));
    axB = new Vector2((float)(-uy * minorImg), (float)(ux * minorImg));
    return true;
  }

  private static bool Inverse3x3(ReadOnlySpan<double> m, Span<double> inv)
  {
    double det = m[0] * (m[4] * m[8] - m[5] * m[7])
               - m[1] * (m[3] * m[8] - m[5] * m[6])
               + m[2] * (m[3] * m[7] - m[4] * m[6]);
    if (Math.Abs(det) < 1e-12) return false;
    double inv1 = 1.0 / det;
    inv[0] = (m[4] * m[8] - m[5] * m[7]) * inv1;
    inv[1] = (m[2] * m[7] - m[1] * m[8]) * inv1;
    inv[2] = (m[1] * m[5] - m[2] * m[4]) * inv1;
    inv[3] = (m[5] * m[6] - m[3] * m[8]) * inv1;
    inv[4] = (m[0] * m[8] - m[2] * m[6]) * inv1;
    inv[5] = (m[2] * m[3] - m[0] * m[5]) * inv1;
    inv[6] = (m[3] * m[7] - m[4] * m[6]) * inv1;
    inv[7] = (m[1] * m[6] - m[0] * m[7]) * inv1;
    inv[8] = (m[0] * m[4] - m[1] * m[3]) * inv1;
    return true;
  }

  // x^3 + a2 x^2 + a1 x + a0 = 0. Retorna 1 ou 3 raizes reais.
  private static int SolveCubic(double a2, double a1, double a0, Span<double> roots)
  {
    double p = a1 - a2 * a2 / 3.0;
    double q = 2 * a2 * a2 * a2 / 27.0 - a2 * a1 / 3.0 + a0;
    double shift = -a2 / 3.0;

    if (Math.Abs(p) < 1e-14)
    {
      roots[0] = Math.Cbrt(-q) + shift;
      return 1;
    }

    double disc = -4 * p * p * p - 27 * q * q;
    if (disc > 0)
    {
      double m = 2 * Math.Sqrt(-p / 3.0);
      double arg = Math.Clamp(3 * q / (p * m), -1.0, 1.0);
      double theta = Math.Acos(arg) / 3.0;
      roots[0] = m * Math.Cos(theta) + shift;
      roots[1] = m * Math.Cos(theta - 2 * Math.PI / 3.0) + shift;
      roots[2] = m * Math.Cos(theta - 4 * Math.PI / 3.0) + shift;
      return 3;
    }
    else
    {
      double sd = Math.Sqrt(-disc / 108.0);
      double u = Math.Cbrt(-q / 2 + sd);
      double v = Math.Cbrt(-q / 2 - sd);
      roots[0] = u + v + shift;
      return 1;
    }
  }

  // Vetor nulo de (M - lambda I). Pega o cross product das 2 linhas mais
  // linearmente independentes.
  private static bool Eigenvector3x3(ReadOnlySpan<double> M, double lambda, Span<double> v)
  {
    double a00 = M[0] - lambda, a01 = M[1], a02 = M[2];
    double a10 = M[3], a11 = M[4] - lambda, a12 = M[5];
    double a20 = M[6], a21 = M[7], a22 = M[8] - lambda;

    double c01x = a01 * a12 - a02 * a11;
    double c01y = a02 * a10 - a00 * a12;
    double c01z = a00 * a11 - a01 * a10;
    double m01 = c01x * c01x + c01y * c01y + c01z * c01z;

    double c02x = a01 * a22 - a02 * a21;
    double c02y = a02 * a20 - a00 * a22;
    double c02z = a00 * a21 - a01 * a20;
    double m02 = c02x * c02x + c02y * c02y + c02z * c02z;

    double c12x = a11 * a22 - a12 * a21;
    double c12y = a12 * a20 - a10 * a22;
    double c12z = a10 * a21 - a11 * a20;
    double m12 = c12x * c12x + c12y * c12y + c12z * c12z;

    double best = m01;
    v[0] = c01x; v[1] = c01y; v[2] = c01z;
    if (m02 > best) { best = m02; v[0] = c02x; v[1] = c02y; v[2] = c02z; }
    if (m12 > best) { best = m12; v[0] = c12x; v[1] = c12y; v[2] = c12z; }

    if (best < 1e-20) return false;
    double inv = 1.0 / Math.Sqrt(best);
    v[0] *= inv; v[1] *= inv; v[2] *= inv;
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
