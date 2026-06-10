using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using Silk.NET.OpenGL;
using Coomer.Features.Rendering;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Text;

public sealed unsafe class TextRenderer : IDisposable
{
  private const int MaxCache = 96;

  private sealed class Entry
  {
    public uint Texture;
    public int Width;
    public int Height;
    public int LastFrame;
  }

  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;
  private readonly Dictionary<(string, int), Entry> _cache = new();
  private int _frame;

  public TextRenderer(GL gl)
  {
    _gl = gl;
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

  public void NewFrame() => _frame++;

  public (int w, int h) Measure(string text, int sizePx)
  {
    var entry = GetOrCreate(text, sizePx);
    return (entry.Width, entry.Height);
  }

  public void Draw(string text, int sizePx, Vector2 pos, Vector2 windowSize, Vector4 color)
  {
    if (string.IsNullOrEmpty(text)) return;
    var entry = GetOrCreate(text, sizePx);

    _shader.Use();
    _shader.SetVec2("windowSize", windowSize);
    _shader.SetVec4("uColor", color);
    _gl.ActiveTexture(TextureUnit.Texture0);
    _gl.BindTexture(TextureTarget.Texture2D, entry.Texture);

    float x0 = pos.X, y0 = pos.Y;
    float x1 = pos.X + entry.Width, y1 = pos.Y + entry.Height;
    Span<float> v = stackalloc float[24]
    {
      x0, y0, 0f, 0f,
      x1, y0, 1f, 0f,
      x1, y1, 1f, 1f,
      x0, y0, 0f, 0f,
      x1, y1, 1f, 1f,
      x0, y1, 0f, 1f,
    };

    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, v);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
    _gl.Disable(EnableCap.Blend);
  }

  private Entry GetOrCreate(string text, int sizePx)
  {
    var key = (text, sizePx);
    if (_cache.TryGetValue(key, out var e))
    {
      e.LastFrame = _frame;
      return e;
    }

    if (_cache.Count >= MaxCache) EvictOldest();

    var font = IosevkaFont.Get(sizePx);
    SizeF measured;
    using (var probe = new Bitmap(1, 1))
    using (var g0 = Graphics.FromImage(probe))
    {
      g0.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
      measured = g0.MeasureString(text, font, int.MaxValue, StringFormat.GenericTypographic);
    }

    int w = Math.Max(1, (int)Math.Ceiling(measured.Width) + 2);
    int h = Math.Max(1, (int)Math.Ceiling(measured.Height));

    byte[] bgra;
    using (var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
    {
      using (var g = Graphics.FromImage(bmp))
      {
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(Color.White);
        g.DrawString(text, font, brush, 0, 0, StringFormat.GenericTypographic);
      }
      var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
      bgra = new byte[w * h * 4];
      System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bgra, 0, bgra.Length);
      bmp.UnlockBits(data);
    }

    uint tex = _gl.GenTexture();
    _gl.BindTexture(TextureTarget.Texture2D, tex);
    fixed (byte* p = bgra)
    {
      _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
          (uint)w, (uint)h, 0, Silk.NET.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, p);
    }
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

    var entry = new Entry { Texture = tex, Width = w, Height = h, LastFrame = _frame };
    _cache[key] = entry;
    return entry;
  }

  private void EvictOldest()
  {
    (string, int) oldestKey = default;
    int oldestFrame = int.MaxValue;
    foreach (var (k, v) in _cache)
    {
      if (v.LastFrame < oldestFrame) { oldestFrame = v.LastFrame; oldestKey = k; }
    }
    if (_cache.TryGetValue(oldestKey, out var e))
    {
      _gl.DeleteTexture(e.Texture);
      _cache.Remove(oldestKey);
    }
  }

  public void Dispose()
  {
    foreach (var e in _cache.Values) _gl.DeleteTexture(e.Texture);
    _cache.Clear();
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteBuffer(_vbo);
    _shader.Dispose();
  }
}
