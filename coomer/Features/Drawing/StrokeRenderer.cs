using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Coomer.Features.Capture;
using Coomer.Features.Lighting;
using Coomer.Features.Navigation;
using Coomer.Features.Rendering;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Drawing;

// AA por distance-field (distSeg + fwidth). Image-space pros tracos/stamps,
// screen-space pro ringue do brush, swatches e rect de region.
public sealed unsafe class StrokeRenderer : IDisposable
{
  // pos.xy, segA.xy, segB.xy, halfWidth
  private const int FloatsPerVertex = 7;
  private const int Stride = FloatsPerVertex * sizeof(float);
  private const float AaPad = 2f;

  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;
  private uint _capacity;
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

  public void Draw(DrawTool tool, Camera camera, bool mirror, Vector2 windowSize,
                   Screenshot shot, Vector2 cursorScreen, ColorHistory? history,
                   RegionExporter? exporter)
  {
    bool wantsBrushRing = tool.IsEnabled && !tool.StampMode && !tool.StickerMode;
    bool wantsStamps = !tool.Hide && tool.Stamps.Count > 0;
    bool wantsStrokes = !tool.Hide && tool.Strokes.Count > 0;
    bool wantsHistory = history != null && history.Entries.Count > 0;
    if (!wantsBrushRing && !wantsStamps && !wantsStrokes && !wantsHistory)
      return;

    var screenshotSize = new Vector2(shot.Width, shot.Height);

    _shader.Use();
    _shader.SetVec2("cameraPos", camera.Position);
    _shader.SetFloat("cameraScale", camera.Scale);
    _shader.SetVec2("windowSize", windowSize);
    _shader.SetVec2("screenshotSize", screenshotSize);
    _shader.SetInt("mirror", mirror ? 1 : 0);

    bool invertActive = exporter != null && exporter.Dragging;
    _shader.SetInt("invertRect", invertActive ? 1 : 0);
    _shader.SetVec2("fragWindowSize", windowSize);
    if (invertActive)
    {
      var a = exporter!.Start;
      var b = exporter.End;
      _shader.SetVec2("invertMin", new Vector2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y)));
      _shader.SetVec2("invertMax", new Vector2(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y)));
    }

    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

    _shader.SetInt("uScreenSpace", 0);
    if (wantsStrokes)
    {
      foreach (var s in tool.Strokes)
      {
        _verts.Clear();
        BuildStroke(s, _verts);
        if (_verts.Count > 0) UploadAndDraw(_verts, s.Color);
      }
    }
    if (wantsStamps)
    {
      foreach (var s in tool.Stamps)
      {
        _verts.Clear();
        EmitSeg(s.Center, s.Center, s.Radius, _verts);
        UploadAndDraw(_verts, s.Color);

        _verts.Clear();
        EmitNumber(s.Center, s.Radius, s.Number, _verts);
        if (_verts.Count > 0) UploadAndDraw(_verts, Contrasting(s.Color));
      }
    }

    _shader.SetInt("uScreenSpace", 1);
    if (wantsBrushRing)
    {
      _verts.Clear();
      // Ringue em screen-space pra espessura constante. raio_tela = raio_imagem * scale.
      float ringRadius = tool.Thickness * 0.5f * camera.Scale;
      BuildRing(cursorScreen, ringRadius, 0.8f, _verts);
      if (_verts.Count > 0)
      {
        var c = tool.CurrentColor;
        UploadAndDraw(_verts, new Vector4(c.X, c.Y, c.Z, 0.75f));
      }
    }

    if (wantsHistory)
    {
      DrawColorSwatches(history!, windowSize);
    }

    _shader.SetInt("uScreenSpace", 0);
    _gl.Disable(EnableCap.Blend);
  }

  private void DrawColorSwatches(ColorHistory history, Vector2 windowSize)
  {
    const float sw = 24f;
    const float gap = 6f;
    const float pad = 16f;

    int n = history.Entries.Count;
    float totalW = n * sw + (n - 1) * gap;
    float x0 = windowSize.X - pad - totalW;
    float y0 = windowSize.Y - pad - sw;

    int i = 0;
    foreach (var c in history.Entries)
    {
      _verts.Clear();
      float cx = x0 + i * (sw + gap) + sw * 0.5f;
      float cy = y0 + sw * 0.5f;
      EmitFilledSquare(cx, cy, sw * 0.5f, _verts);
      UploadAndDraw(_verts, c);

      _verts.Clear();
      BuildRectOutline(new Vector2(cx - sw / 2, cy - sw / 2),
                       new Vector2(cx + sw / 2, cy + sw / 2), 0.8f, _verts);
      UploadAndDraw(_verts, new Vector4(0f, 0f, 0f, 0.6f));
      i++;
    }
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
        if (s.Points.Count == 1) { EmitSeg(s.Points[0], s.Points[0], h, verts); break; }
        if (s.Points.Count == 2) { EmitSeg(s.Points[0], s.Points[1], h, verts); break; }
        EmitSmoothFree(s.Points, h, verts);
        break;

      case DrawShape.Line:
        if (s.Points.Count >= 2)
          EmitSeg(s.Points[0], s.Points[1], h, verts);
        break;

      case DrawShape.Arrow:
        if (s.Points.Count == 1) { EmitSeg(s.Points[0], s.Points[0], h, verts); break; }
        if (s.Points.Count == 2) { EmitArrow(s.Points[0], s.Points[1], h, s.Thickness, verts); break; }
        EmitFreehandArrow(s.Points, h, s.Thickness, verts);
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
        // 3 pontos = elipse via PCA. 2 pontos = bbox -> elipse axis-aligned.
        if (s.Points.Count >= 3)
        {
          var center = s.Points[0];
          var axA = s.Points[1] - center;
          var axB = s.Points[2] - center;
          EmitEllipse(center, axA, axB, h, verts);
        }
        else if (s.Points.Count >= 2)
        {
          var a = s.Points[0];
          var b = s.Points[1];
          var center = (a + b) * 0.5f;
          var axA = new Vector2((b.X - a.X) * 0.5f, 0f);
          var axB = new Vector2(0f, (b.Y - a.Y) * 0.5f);
          EmitEllipse(center, axA, axB, h, verts);
        }
        break;
    }
  }

  private static void EmitFreehandArrow(List<Vector2> pts, float h, float thickness, List<float> v)
  {
    EmitSmoothFree(pts, h, v);

    float totalLen = 0f;
    for (int i = 1; i < pts.Count; i++) totalLen += (pts[i] - pts[i - 1]).Length();

    // Head proporcional, capada em 30% do path. Pula a cabeca se o path nao
    // tem sequer 2x a cabeca de comprimento — evita seta zoada em rabisco curto.
    float baseHead = MathF.Max(thickness * 6f, 18f);
    float headLen = MathF.Min(baseHead, totalLen * 0.30f);
    if (totalLen < headLen * 2f) return;

    // Tangente: caminha headLen pra tras ao longo do path (nao linha reta) pra
    // amaciar jitter das ultimas amostras e seguir a curvatura real.
    var end = pts[^1];
    Vector2 from = end;
    float walked = 0f;
    for (int i = pts.Count - 2; i >= 0; i--)
    {
      walked += (pts[i + 1] - pts[i]).Length();
      from = pts[i];
      if (walked >= headLen) break;
    }
    var d = end - from;
    float len = d.Length();
    if (len < thickness) return;
    var dir = d / len;

    const float theta = 0.5f;
    float cs = MathF.Cos(theta), sn = MathF.Sin(theta);
    var backDir = -dir;
    var leftDir = new Vector2(backDir.X * cs - backDir.Y * sn, backDir.X * sn + backDir.Y * cs);
    var rightDir = new Vector2(backDir.X * cs + backDir.Y * sn, -backDir.X * sn + backDir.Y * cs);
    EmitSeg(end, end + leftDir * headLen, h, v);
    EmitSeg(end, end + rightDir * headLen, h, v);
  }

  // p(theta) = center + cos(theta)*axA + sin(theta)*axB
  private static void EmitEllipse(Vector2 center, Vector2 axA, Vector2 axB, float h, List<float> v)
  {
    float majorMag = axA.Length();
    float minorMag = axB.Length();
    if (majorMag < 1f && minorMag < 1f) return;
    float maxR = MathF.Max(majorMag, minorMag);
    int n = Math.Clamp(40 + (int)(maxR * 0.4f), 48, 160);
    float dPhi = MathF.PI * 2f / n;
    var prev = center + axA; // theta = 0
    for (int i = 1; i <= n; i++)
    {
      float ang = i * dPhi;
      var cur = center + axA * MathF.Cos(ang) + axB * MathF.Sin(ang);
      EmitSeg(prev, cur, h, v);
      prev = cur;
    }
  }

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

  private static void EmitArrow(Vector2 a, Vector2 b, float h, float thickness, List<float> v)
  {
    var d = b - a;
    float len = d.Length();
    if (len < 1f) { EmitSeg(a, b, h, v); return; }
    var dir = d / len;
    EmitSeg(a, b, h, v);

    float maxHead = len * 0.50f;
    float floor = MathF.Min(thickness * 6f, maxHead);
    float headLen = Math.Clamp(len * 0.30f, floor, maxHead);
    const float theta = 0.5f;
    float cs = MathF.Cos(theta), sn = MathF.Sin(theta);
    var backDir = -dir;
    var leftDir = new Vector2(backDir.X * cs - backDir.Y * sn, backDir.X * sn + backDir.Y * cs);
    var rightDir = new Vector2(backDir.X * cs + backDir.Y * sn, -backDir.X * sn + backDir.Y * cs);
    EmitSeg(b, b + leftDir * headLen, h, v);
    EmitSeg(b, b + rightDir * headLen, h, v);
  }

  // Numero em 7-segment-display centrado no stamp. Suporta 1-99 (>99 vira "99").
  private static void EmitNumber(Vector2 center, float stampRadius, int number, List<float> v)
  {
    int n = Math.Clamp(number, 0, 99);
    string s = n.ToString();
    float r = stampRadius;
    float digW = r * 0.5f;
    float digH = r * 0.7f;
    float strokeH = MathF.Max(1.5f, r * 0.12f);
    float gap = r * 0.15f;

    float totalW = s.Length * digW + (s.Length - 1) * gap;
    float startX = center.X - totalW * 0.5f + digW * 0.5f;
    for (int i = 0; i < s.Length; i++)
    {
      int d = s[i] - '0';
      float cx = startX + i * (digW + gap);
      EmitDigit(new Vector2(cx, center.Y), digW, digH, strokeH, d, v);
    }
  }

  private static void EmitDigit(Vector2 c, float w, float h, float sh, int d, List<float> v)
  {
    float hw = w * 0.5f;
    float hh = h * 0.5f;
    var tl = new Vector2(c.X - hw, c.Y - hh);
    var tr = new Vector2(c.X + hw, c.Y - hh);
    var ml = new Vector2(c.X - hw, c.Y);
    var mr = new Vector2(c.X + hw, c.Y);
    var bl = new Vector2(c.X - hw, c.Y + hh);
    var br = new Vector2(c.X + hw, c.Y + hh);

    bool a, b, cc, dd, e, f, g;
    switch (d)
    {
      case 0: a = b = cc = dd = e = f = true; g = false; break;
      case 1: a = dd = e = f = g = false; b = cc = true; break;
      case 2: a = b = g = e = dd = true; cc = f = false; break;
      case 3: a = b = g = cc = dd = true; e = f = false; break;
      case 4: f = g = b = cc = true; a = dd = e = false; break;
      case 5: a = f = g = cc = dd = true; b = e = false; break;
      case 6: a = f = g = cc = dd = e = true; b = false; break;
      case 7: a = b = cc = true; dd = e = f = g = false; break;
      case 8: a = b = cc = dd = e = f = g = true; break;
      case 9: a = b = cc = dd = f = g = true; e = false; break;
      default: return;
    }

    if (a) EmitSeg(tl, tr, sh, v);
    if (b) EmitSeg(tr, mr, sh, v);
    if (cc) EmitSeg(mr, br, sh, v);
    if (dd) EmitSeg(bl, br, sh, v);
    if (e) EmitSeg(ml, bl, sh, v);
    if (f) EmitSeg(tl, ml, sh, v);
    if (g) EmitSeg(ml, mr, sh, v);
  }

  private static Vector4 Contrasting(Vector4 c)
  {
    float l = 0.299f * c.X + 0.587f * c.Y + 0.114f * c.Z;
    return l > 0.55f ? new Vector4(0f, 0f, 0f, 1f) : new Vector4(1f, 1f, 1f, 1f);
  }

  private static void BuildRing(Vector2 center, float radius, float strokeHalf, List<float> v)
  {
    if (radius < 0.5f) return;
    int n = Math.Clamp(16 + (int)(radius * 0.6f), 24, 96);
    float dPhi = MathF.PI * 2f / n;
    var prev = center + new Vector2(radius, 0f);
    for (int i = 1; i <= n; i++)
    {
      float ang = i * dPhi;
      var cur = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
      EmitSeg(prev, cur, strokeHalf, v);
      prev = cur;
    }
  }

  private static void BuildRectOutline(Vector2 a, Vector2 b, float strokeHalf, List<float> v)
  {
    var tl = new Vector2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y));
    var br = new Vector2(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y));
    var tr = new Vector2(br.X, tl.Y);
    var bl = new Vector2(tl.X, br.Y);
    EmitSeg(tl, tr, strokeHalf, v);
    EmitSeg(tr, br, strokeHalf, v);
    EmitSeg(br, bl, strokeHalf, v);
    EmitSeg(bl, tl, strokeHalf, v);
  }

  // Segmento horizontal curto com halfWidth=halfSide vira "stadium" (cantos
  // arredondados pelo distSeg) — visualmente um quadrado pro swatch.
  private static void EmitFilledSquare(float cx, float cy, float halfSide, List<float> v)
  {
    var left = new Vector2(cx - halfSide * 0.5f, cy);
    var right = new Vector2(cx + halfSide * 0.5f, cy);
    EmitSeg(left, right, halfSide, v);
  }

  private static void EmitSeg(Vector2 a, Vector2 b, float h, List<float> v)
  {
    var d = b - a;
    float len = d.Length();
    Vector2 dir, n;
    if (len < 1e-4f)
    {
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
