using System.Diagnostics;
using System.Numerics;
using Coomer.Features.Capture;
using Coomer.Features.Navigation;

namespace Coomer.Features.Drawing;

/// <summary>
/// Estado do modo de desenho. Quando ativo, o left-drag deixa de panar e vira
/// pincel/linha/seta/retangulo/circulo. Tracos sao guardados em coords de imagem
/// (pixel da screenshot), com mirror "desfeito" no clique e refeito no shader
/// — assim o desenho fica preso ao conteudo independente do estado do espelho.
///
/// No release de um traco livre (Free), tenta reconhecer um circulo desenhado
/// a mao (a la iPhone Scribble): se a forma fechada parece circular o bastante,
/// troca o traco por um Circle perfeito.
/// </summary>
public sealed class DrawTool
{
  // Paleta minima: bem saturada pra constrastar com a foto por baixo.
  private static readonly Vector4[] Pallet =
  {
    new(1.00f, 0.20f, 0.20f, 1f), // vermelho
    new(1.00f, 0.85f, 0.20f, 1f), // amarelo
    new(0.25f, 1.00f, 0.40f, 1f), // verde
    new(0.30f, 0.70f, 1.00f, 1f), // azul
    new(1.00f, 0.40f, 1.00f, 1f), // magenta
    new(1.00f, 1.00f, 1.00f, 1f), // branco
    new(0.00f, 0.00f, 0.00f, 1f), // preto
  };

  public bool IsEnabled;
  public DrawShape Shape = DrawShape.Free;
  public int ColorIndex;
  /// <summary>Espessura em pixels de imagem.</summary>
  public float Thickness = 4f;
  /// <summary>Se ativado, traco livre que parece circulo vira circulo perfeito no release.</summary>
  public bool AutoCircle = true;
  public readonly List<Stroke> Strokes = new();

  private Stroke? _active;
  private readonly OneEuroFilterV2 _filter = new();
  private readonly Stopwatch _watch = new();

  public Vector4 CurrentColor => Pallet[ColorIndex];

  public void Begin(Vector2 cursorScreen, Vector2 windowSize, Screenshot shot,
                    Camera camera, bool mirror)
  {
    _filter.Reset();
    _watch.Restart();
    // Primeira amostra: passe direto (filtro inicializado nesse ponto sem lag).
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

    if (_active.Shape == DrawShape.Free)
    {
      // Em coords de imagem 0.5px e nada � descarta pontos colados para nao inchar
      // o buffer com microamostras do mouse de 1000Hz.
      if (Vector2.DistanceSquared(_active.Points[^1], p) < 0.25f) return;
      _active.Points.Add(p);
    }
    else
    {
      // Linha/seta/retangulo/circulo: so 2 pontos (inicio + arrasto atual).
      if (_active.Points.Count < 2) _active.Points.Add(p);
      else _active.Points[1] = p;
    }
  }

  public void End()
  {
    if (_active != null
        && _active.Shape == DrawShape.Free
        && AutoCircle
        && TryDetectCircle(_active.Points, out var c, out var r))
    {
      // Efeito "Scribble" do iPhone: se o que voce desenhou ja era pra ser um
      // circulo, vira um circulo bonito.
      _active.Shape = DrawShape.Circle;
      _active.Points.Clear();
      _active.Points.Add(c);
      _active.Points.Add(c + new Vector2(r, 0f)); // ponto na borda
    }
    _active = null;
  }

  public void Undo()
  {
    if (Strokes.Count == 0) return;
    Strokes.RemoveAt(Strokes.Count - 1);
    _active = null;
  }

  public void Clear()
  {
    Strokes.Clear();
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

  // Heuristica de reconhecimento de circulo (auto-correcao do traco livre).
  // Criterios em ordem (qualquer falha => nao e circulo):
  //   1. >=16 pontos amostrados
  //   2. Raio medio >= 12 px (nao vira "circulo" qualquer tap/jitter)
  //   3. stdDev(raio)/media <= 0.22 (todos os pontos a distancia parecida do centro)
  //   4. Quase fechado: dist(p0, pN) < 0.6 * raio medio
  //   5. Volta cumulativa >= ~306 graus (evita "C aberto")
  // Centro = centroide; raio = distancia media ao centroide.
  private static bool TryDetectCircle(List<Vector2> pts, out Vector2 center, out float radius)
  {
    center = default;
    radius = 0f;
    if (pts.Count < 16) return false;

    Vector2 c = Vector2.Zero;
    foreach (var p in pts) c += p;
    c /= pts.Count;

    float sumR = 0f, sumR2 = 0f;
    foreach (var p in pts)
    {
      float r = (p - c).Length();
      sumR += r;
      sumR2 += r * r;
    }
    float meanR = sumR / pts.Count;
    if (meanR < 12f) return false;

    float varR = sumR2 / pts.Count - meanR * meanR;
    float stdR = MathF.Sqrt(MathF.Max(0f, varR));
    if (stdR / meanR > 0.22f) return false;

    float closeDist = (pts[0] - pts[^1]).Length();
    if (closeDist > meanR * 0.6f) return false;

    float totalAngle = 0f;
    float prevA = MathF.Atan2(pts[0].Y - c.Y, pts[0].X - c.X);
    for (int i = 1; i < pts.Count; i++)
    {
      float a = MathF.Atan2(pts[i].Y - c.Y, pts[i].X - c.X);
      float diff = a - prevA;
      if (diff > MathF.PI) diff -= 2f * MathF.PI;
      if (diff < -MathF.PI) diff += 2f * MathF.PI;
      totalAngle += diff;
      prevA = a;
    }
    if (MathF.Abs(totalAngle) < MathF.PI * 1.7f) return false; // < ~306 graus

    center = c;
    radius = meanR;
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