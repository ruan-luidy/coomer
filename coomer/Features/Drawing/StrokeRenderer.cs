using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Coomer.Features.Navigation;
using Coomer.Features.Rendering;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Drawing;

/// <summary>
/// Renderiza os tracos do <see cref="DrawTool"/> por cima da screensho. Cada
/// segmento vira um quad (par de triangulos) extrudido perpendicular pela meia
/// essura, com cap nas pontas pra mascarar a juncao entre segmentos. Os
/// vertices vivem em coords de IMAGEM e usam o mesmo vert transform do screenshot
/// quad � entao pan/zoom levam o desenho junto sem nenhum trabalho extra.
/// </summary>
public sealed unsafe class StrokeRenderer : IDisposable
{
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
      new[] { (0u, "aPos") });

    _vao = gl.GenVertexArray();
    gl.BindVertexArray(_vao);

    _vbo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false,
                            2 * sizeof(float), (void*)0);
    gl.EnableVertexAttribArray(0);
  }

  public void Draw(Coomer.Features.Drawing.DrawTool tool, Camera camera, bool mirror,
                   Vector2 windowSize, Vector2 screenshotSize)
  {
    if (tool.Strokes.Count == 0) return;

    _shader.Use();
    _shader.SetVec2("cameraPos", camera.Position);
    _shader.SetFloat("cameraScale", camera.Scale);
    _shader.SetVec2("windowSize", windowSize);
    _shader.SetVec2("screenshotSize", screenshotSize);
    _shader.SetInt("mirror", mirror ? 1 : 0);

    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    // Blend pro alpha (default opaco hoje, mas deixa pronto pra cores translucidas).
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

    foreach (var s in tool.Strokes)
    {
      _verts.Clear();
      Build(s, _verts);
      if (_verts.Count == 0) continue;
      UploadAndDraw(_verts, s.Color);
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
    _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(verts.Count / 2));
  }

  private static void Build(Stroke s, List<float> verts)
  {
    if (s.Points.Count == 0) return;
    float h = s.Thickness * 0.5f;

    switch (s.Shape)
    {
      case DrawShape.Free:
        if (s.Points.Count == 1)
        {
          EmitDot(s.Points[0], h, verts);
          break;
        }
        for (int i = 0; i < s.Points.Count - 1; i++)
          EmitQuad(s.Points[i], s.Points[i + 1], h, verts);
        break;

      case DrawShape.Line:
        if (s.Points.Count >= 2)
          EmitQuad(s.Points[0], s.Points[1], h, verts);
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
          EmitQuad(tl, tr, h, verts);
          EmitQuad(tr, br, h, verts);
          EmitQuad(br, bl, h, verts);
          EmitQuad(bl, tl, h, verts);
        }
        break;
    }
  }

  // Quad com cap nas pontas (extende as 2 extremidades por +h ao longo do 
  // proprio segmento). Isso faz dois segmentos consecutivos do pincel se 
  // sobrepoem em ~h, mascarando a "agulhinha" que sobra na juncao sem precisar
  // calcular miter � bom o bastante pra tracos suavizados (curvatura baixa).
  private static void EmitQuad(Vector2 a, Vector2 b, float h, List<float> v)
  {
    var d = b - a;
    float len = d.Length();
    if (len < 1e-4f) return;
    var dir = d / len;
    var n = new Vector2(-dir.Y, dir.X) * h;
    var a0 = a - dir * h;
    var b0 = b + dir * h;
    var p1 = a0 + n;
    var p2 = a0 - n;
    var p3 = b0 + n;
    var p4 = b0 - n;
    // tri 1: p1, p2, p3 | tru 2: p2, p4, p3
    v.Add(p1.X); v.Add(p1.Y);
    v.Add(p2.X); v.Add(p2.Y);
    v.Add(p3.X); v.Add(p3.Y);
    v.Add(p2.X); v.Add(p2.Y);
    v.Add(p4.X); v.Add(p4.Y);
    v.Add(p3.X); v.Add(p3.Y);
  }

  // Ponto isolado (click sem arrasto)> quadradinho axis-aligned do tamnho da espessura.
  private static void EmitDot(Vector2 p, float h, List<float> v)
  {
    var p1 = p + new Vector2(-h, -h);
    var p2 = p + new Vector2(h, -h);
    var p3 = p + new Vector2(h, h);
    var p4 = p + new Vector2(-h, h);
    v.Add(p1.X); v.Add(p1.Y);
    v.Add(p2.X); v.Add(p2.Y);
    v.Add(p3.X); v.Add(p3.Y);
    v.Add(p1.X); v.Add(p1.Y);
    v.Add(p3.X); v.Add(p3.Y);
    v.Add(p4.X); v.Add(p4.Y);
  }

  public void Dispose()
  {
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteBuffer(_vbo);
    _shader.Dispose();
  }
}