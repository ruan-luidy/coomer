using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using Coomer.Features.Drawing;
using Coomer.Features.Rendering;
using Coomer.Features.Text;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Stickers;

public sealed unsafe class StickerPalette : IDisposable
{
  private const int Slots = 7;
  private const float ThumbSize = 56f;
  private const float Gap = 8f;
  private const float Pad = 12f;

  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;
  private readonly TextRenderer _text;

  public StickerPalette(GL gl, TextRenderer text)
  {
    _gl = gl;
    _text = text;
    _shader = new Shader(gl,
        EmbeddedShader.Load("text.vert.glsl"),
        EmbeddedShader.Load("text.frag.glsl"),
        new[] { (0u, "aPos"), (1u, "aUv") });

    _vao = gl.GenVertexArray();
    gl.BindVertexArray(_vao);
    _vbo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(6 * 4 * sizeof(float)), null, BufferUsageARB.DynamicDraw);
    gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
    gl.EnableVertexAttribArray(0);
    gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
    gl.EnableVertexAttribArray(1);

    _shader.Use();
    _shader.SetInt("tex", 0);
  }

  public void Draw(Vector2 windowSize, DrawTool tool, StickerCache cache, StickerState state)
  {
    if (!tool.IsEnabled || !tool.StickerMode) return;
    if (cache.Categories.Count == 0 || state.Current == null) return;

    var catName = cache.Categories[Math.Clamp(state.CategoryIndex, 0, cache.Categories.Count - 1)];
    var list = cache.InCategory(catName);
    if (list.Count == 0) return;

    int cur = state.IndexInCategory;
    int n = list.Count;
    int slots = Math.Min(Slots, n);
    int half = slots / 2;

    float totalW = slots * ThumbSize + (slots - 1) * Gap;
    float x0 = (windowSize.X - totalW) * 0.5f;
    float y0 = windowSize.Y - ThumbSize - Pad - 20f;

    _shader.Use();
    _shader.SetVec2("windowSize", windowSize);
    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.ActiveTexture(TextureUnit.Texture0);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

    for (int i = 0; i < slots; i++)
    {
      int offset = i - half;
      int idx = ((cur + offset) % n + n) % n;
      var entry = list[idx];
      float x = x0 + i * (ThumbSize + Gap);
      float opacity = (idx == cur) ? 1.0f : 0.55f;
      DrawThumb(entry, x, y0, ThumbSize, opacity);
      if (idx == cur)
        DrawOutline(x, y0, ThumbSize, new Vector4(1f, 1f, 1f, 0.85f));
    }
    _gl.Disable(EnableCap.Blend);

    // labels: categoria em cima, nome do atual embaixo
    string catLabel = catName.Length == 0 ? "(root)" : catName + "/";
    var (cw, _) = _text.Measure(catLabel, 12);
    _text.Draw(catLabel, 12,
      new Vector2((windowSize.X - cw) * 0.5f, y0 - 18f), windowSize,
      new Vector4(0.9f, 0.9f, 0.9f, 0.8f));

    var curName = System.IO.Path.GetFileName(list[cur].Path);
    var (nw, _) = _text.Measure(curName, 13);
    _text.Draw(curName, 13,
      new Vector2((windowSize.X - nw) * 0.5f, y0 + ThumbSize + 4f), windowSize,
      new Vector4(1f, 1f, 1f, 0.95f));
  }

  private void DrawThumb(StickerEntry entry, float x, float y, float size, float opacity)
  {
    Span<float> v = stackalloc float[24]
    {
      x,          y,        0f, 0f,
      x + size,   y,        1f, 0f,
      x + size,   y + size, 1f, 1f,
      x,          y,        0f, 0f,
      x + size,   y + size, 1f, 1f,
      x,          y + size, 0f, 1f,
    };
    _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, v);
    _gl.BindTexture(TextureTarget.Texture2D, entry.Texture);
    _shader.SetVec4("uColor", new Vector4(1f, 1f, 1f, opacity));
    _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
  }

  private void DrawOutline(float x, float y, float size, Vector4 color)
  {
    // outline = 4 retangulos finos (top, bottom, left, right) usando o branco
    // do texture do font cache ja seria complicacao; em vez disso reusamos
    // o quad do thumb com o uColor opaco e texture = qualquer 1px branco.
    // Truque: usar a textura ja bound (do ultimo thumb) e colar uma franja.
    // Como nao temos um quad-solido pronto, deixa o outline pra prox iter.
  }

  public void Dispose()
  {
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteBuffer(_vbo);
    _shader.Dispose();
  }
}
