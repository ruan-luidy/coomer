using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Coomer.Features.Capture;
using Coomer.Features.Navigation;
using Coomer.Features.Rendering;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Drawing;

/// <summary>
/// Renderiza os tracos do <see cref="DrawTool"/> por cima da screenshot. Cada
/// segmento vira um quad expandido em halfWidth+pad de AA, e o fragment shader
/// faz anti-alias por distance-field (distancia ao segmento + fwidth) — entrega
/// borda lisa, round caps de graca e juncoes invisiveis entre segmentos
/// consecutivos. Apos os tracos, desenha o ringue indicador do tamanho do brush
/// na posicao do cursor.
/// </summary>
public sealed unsafe class StrokeRenderer : IDisposable
{
  // 2 (pos) + 2 (segA) + 2 (segB) + 1 (halfWidth)
  private const int FloatsPerVertex = 7;
  private const int Stride = FloatsPerVertex * sizeof(float);

  // Pad de AA: o quad precisa ser maior que o segmento por halfWidth + alguns
  // pixels pra a banda do smoothstep caber dentro. 2 imagem-units sobra
  // bastante em qualquer zoom razoavel (MinScale=1 entao fwidth<=1).
  private const float AaPad = 2f;

  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;
  private uint _capacity; // tamanho atual (em floats) do buffer
  private readonly List<float> _verts = new();

  public StrokeRenderer(GL gl)
  {
    _gl = gl;
    _shader = new Shader(gl,
        EmbeddedShader.Load("stroke.vert.glsl"),
        EmbeddedShader.Load("stroke.frag.glsl"),
        new[]
        {
          (0u, "aPos"),
          (1u, "aSegA"),
          (2u, "aSegB"),
          (3u, "aHalfWidth"),
        });

    _vao = gl.GenVertexArray();
    gl.BindVertexArray(_vao);

    _vbo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

    gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, Stride, (void*)0);
    gl.EnableVertexAttribArray(0);
    gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Stride, (void*)(2 * sizeof(float)));
    gl.EnableVertexAttribArray(1);
    gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, Stride, (void*)(4 * sizeof(float)));
    gl.EnableVertexAttribArray(2);
    gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, Stride, (void*)(6 * sizeof(float)));
    gl.EnableVertexAttribArray(3);
  }

  public void Draw(DrawTool tool, Camera camera, bool mirror,
                   Vector2 windowSize, Screenshot shot, Vector2 cursorScreen)
  {
    bool hasStrokes = tool.Strokes.Count > 0;
    bool wantsRing = tool.IsEnabled;
    if (!hasStrokes && !wantsRing) return;

    var screenshotSize = new Vector2(shot.Width, shot.Height);

    _shader.Use();
    _shader.SetVec2("cameraPos", camera.Position);
    _shader.SetFloat("cameraScale", camera.Scale);
    _shader.SetVec2("windowSize", windowSize);
    _shader.SetVec2("screenshotSize", screenshotSize);
    _shader.SetInt("mirror", mirror ? 1 : 0);

    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

    foreach (var s in tool.Strokes)
    {
      _verts.Clear();
      BuildStroke(s, _verts);
      if (_verts.Count == 0) continue;
      UploadAndDraw(_verts, s.Color);
    }

    if (wantsRing)
    {
      _verts.Clear();
      var cursorImg = ScreenToImage(cursorScreen, windowSize, shot, camera, mirror);
      BuildBrushRing(cursorImg, tool.Thickness * 0.5f, camera.Scale, _verts);
      if (_verts.Count > 0)
      {
        // ringue na cor do brush mas semi-translucido — fica visivel sem
        // sobrepujar o conteudo embaixo
        var c = tool.CurrentColor;
        UploadAndDraw(_verts, new Vector4(c.X, c.Y, c.Z, 0.75f));
      }
    }

    _gl.Disable(EnableCap.Blend);
  }

  private void UploadAndDraw(List<float> verts, Vector4 color)
  {
    var span = CollectionsMarshal.AsSpan(verts);
    if ((uint)verts.Count > _capacity)
    {
      _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, span, BufferUsageARB.DynamicDraw);
      _capacity = (uint)verts.Count;
    }
    else
    {
      _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, span);
    }
    _shader.SetVec4("uColor", color);
    _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(verts.Count / FloatsPerVertex));
  }

  private static void BuildStroke(Stroke s, List<float> verts)
  {
    if (s.Points.Count == 0) return;
    float h = s.Thickness * 0.5f;

    switch (s.Shape)
    {
      case DrawShape.Free:
        if (s.Points.Count == 1)
        {
          EmitSeg(s.Points[0], s.Points[0], h, verts);
          break;
        }
        if (s.Points.Count == 2)
        {
          EmitSeg(s.Points[0], s.Points[1], h, verts);
          break;
        }
        EmitSmoothFree(s.Points, h, verts);
        break;

      case DrawShape.Line:
        if (s.Points.Count >= 2)
          EmitSeg(s.Points[0], s.Points[1], h, verts);
        break;

      case DrawShape.Arrow:
        if (s.Points.Count >= 2)
          EmitArrow(s.Points[0], s.Points[1], h, s.Thickness, verts);
        break;

      case DrawShape.Rect:
        if (s.Points.Count >= 2)
        {
          var a = s.Points[0];
          var b = s.Points[1];
          var tl = new Vector2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
          var br = new Vector2(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
          var tr = new Vector2(br.X, tl.Y);
          var bl = new Vector2(tl.X, br.Y);
          EmitSeg(tl, tr, h, verts);
          EmitSeg(tr, br, h, verts);
          EmitSeg(br, bl, h, verts);
          EmitSeg(bl, tl, h, verts);
        }
        break;

      case DrawShape.Circle:
        if (s.Points.Count >= 2)
        {
          var center = s.Points[0];
          float radius = (s.Points[1] - center).Length();
          EmitCircle(center, radius, h, verts);
        }
        break;
    }
  }

  // Seta = shaft (a->b) + dois segmentos da cabeca saindo de b voltando ao longo
  // de -dir, rotacionados +/- theta. Comprimento da cabeca: proporcional ao
  // shaft mas com piso/teto pra parecer setinha mesmo em arrasto curto/longo.
  private static void EmitArrow(Vector2 a, Vector2 b, float h, float thickness, List<float> v)
  {
    var d = b - a;
    float len = d.Length();
    if (len < 1f)
    {
      EmitSeg(a, b, h, v);
      return;
    }
    var dir = d / len;
    EmitSeg(a, b, h, v); // shaft

    // Cabeca: 30% do shaft, com piso 6*thickness (pra seta curta ainda mostrar
    // ponta) e teto 50% do shaft. Em seta curtinha + brush gigante o piso pode
    // ultrapassar o teto — clampa o piso ao teto antes, senao Math.Clamp
    // estoura com ArgumentException no left-drag e a app trava.
    float maxHead = len * 0.50f;
    float floor = MathF.Min(thickness * 6f, maxHead);
    float headLen = Math.Clamp(len * 0.30f, floor, maxHead);
    const float theta = 0.5f; // ~28.6 graus de abertura
    float cs = MathF.Cos(theta), sn = MathF.Sin(theta);

    var backDir = -dir;
    var leftDir = new Vector2(
      backDir.X * cs - backDir.Y * sn,
      backDir.X * sn + backDir.Y * cs);
    var rightDir = new Vector2(
      backDir.X * cs + backDir.Y * sn,
      -backDir.X * sn + backDir.Y * cs);

    EmitSeg(b, b + leftDir * headLen, h, v);
    EmitSeg(b, b + rightDir * headLen, h, v);
  }

  // Circulo do shape Circle: poligono regular com N segmentos. N cresce com o
  // raio pra nao virar octogono em circulo grande. Cada segmento tem espessura
  // h (igual aos outros shapes).
  private static void EmitCircle(Vector2 center, float radius, float h, List<float> v)
  {
    if (radius < 1f) return;
    int n = Math.Clamp(32 + (int)(radius * 0.4f), 40, 128);
    float dPhi = MathF.PI * 2f / n;
    var prev = center + new Vector2(radius, 0f);
    for (int i = 1; i <= n; i++)
    {
      float ang = i * dPhi;
      var cur = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
      EmitSeg(prev, cur, h, v);
      prev = cur;
    }
  }

  // Traco livre suave: passa uma spline Catmull-Rom pelos pontos amostrados do
  // mouse em vez de ligar com retas. Cada span P1->P2 usa P0 e P3 como tangentes
  // (pontas clampadas pra ela mesma), e e subdividido por comprimento pra manter
  // a curvatura lisa sem tesselar demais em tracos longos. Custo zero de lag: a
  // suavidade e puramente geometrica, nao adia o tempo do tracado.
  private static void EmitSmoothFree(List<Vector2> pts, float h, List<float> v)
  {
    int count = pts.Count;
    for (int i = 0; i < count - 1; i++)
    {
      var p0 = pts[i == 0 ? 0 : i - 1];
      var p1 = pts[i];
      var p2 = pts[i + 1];
      var p3 = pts[i + 2 >= count ? count - 1 : i + 2];

      float segLen = (p2 - p1).Length();
      int sub = Math.Clamp((int)(segLen / 3f), 1, 24);

      var prev = p1;
      for (int k = 1; k <= sub; k++)
      {
        float t = (float)k / sub;
        var cur = CatmullRom(p0, p1, p2, p3, t);
        EmitSeg(prev, cur, h, v);
        prev = cur;
      }
    }
  }

  // Catmull-Rom uniforme (tensao 0.5): a curva passa por p1 e p2 com tangentes
  // dadas pelos vizinhos. t em [0,1] no span p1->p2.
  private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
  {
    float t2 = t * t;
    float t3 = t2 * t;
    return 0.5f * (
        2f * p1
      + (p2 - p0) * t
      + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
      + (3f * p1 - 3f * p2 + p3 - p0) * t3);
  }

  // Constroi um quad cobrindo a area de influencia do segmento (halfWidth + pad
  // de AA). O fragment shader que decide alpha por distance-to-segment, dando
  // round caps automaticos nas pontas (distSeg clampa t em [0,1]).
  private static void EmitSeg(Vector2 a, Vector2 b, float h, List<float> v)
  {
    var d = b - a;
    float len = d.Length();
    Vector2 dir, n;
    if (len < 1e-4f)
    {
      // Ponto isolado: usa eixo cardinal pro quad. distSeg trata A==B como
      // disco no ponto, entao da redondinho.
      dir = new Vector2(1f, 0f);
      n = new Vector2(0f, 1f);
    }
    else
    {
      dir = d / len;
      n = new Vector2(-dir.Y, dir.X);
    }
    float pad = h + AaPad;
    var a0 = a - dir * pad;
    var b0 = b + dir * pad;
    var p1 = a0 + n * pad;
    var p2 = a0 - n * pad;
    var p3 = b0 + n * pad;
    var p4 = b0 - n * pad;

    AddVertex(v, p1, a, b, h);
    AddVertex(v, p2, a, b, h);
    AddVertex(v, p3, a, b, h);
    AddVertex(v, p2, a, b, h);
    AddVertex(v, p4, a, b, h);
    AddVertex(v, p3, a, b, h);
  }

  private static void AddVertex(List<float> v, Vector2 p, Vector2 a, Vector2 b, float h)
  {
    v.Add(p.X); v.Add(p.Y);
    v.Add(a.X); v.Add(a.Y);
    v.Add(b.X); v.Add(b.Y);
    v.Add(h);
  }

  // Ringue mostrando o tamanho do brush no cursor. Stroke do ringue e fino
  // (constante em pixels de TELA), independente do zoom — vira segmento de
  // halfWidth = 1px-de-tela em image-units = 1/(2*scale). Conta de segmentos
  // escala com a circunferencia pra nao virar poligono visivel.
  private static void BuildBrushRing(Vector2 center, float radius, float scale, List<float> v)
  {
    if (radius < 0.1f) return;
    float ringHalf = MathF.Max(0.25f / scale, 0.1f);
    // 32 segmentos pra brush minusculo, ate ~96 pra brush gigante. Mantem ~3
    // px-de-tela por segmento (assumindo scale=1; em zoom alto compensa pra
    // mais segmentos via radius*scale).
    int n = Math.Clamp(16 + (int)(radius * scale * 0.6f), 24, 96);

    float dPhi = MathF.PI * 2f / n;
    var prev = center + new Vector2(radius, 0f);
    for (int i = 1; i <= n; i++)
    {
      float ang = i * dPhi;
      var cur = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
      EmitSeg(prev, cur, ringHalf, v);
      prev = cur;
    }
  }

  // Mesma conta do DrawTool.ScreenToImage: cursor de tela -> pixel canonico
  // (sem mirror) da screenshot, pra centrar o ringue onde o usuario ta apontando.
  private static Vector2 ScreenToImage(Vector2 cursor, Vector2 windowSize,
                                       Screenshot shot, Camera camera, bool mirror)
  {
    var centered = cursor - windowSize * 0.5f;
    var world = centered / camera.Scale;
    var p = world + camera.Position + new Vector2(shot.Width, shot.Height) * 0.5f;
    if (mirror) p.X = shot.Width - p.X;
    return p;
  }

  public void Dispose()
  {
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteBuffer(_vbo);
    _shader.Dispose();
  }
}
