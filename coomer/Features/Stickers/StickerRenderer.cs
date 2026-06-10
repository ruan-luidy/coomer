using System.Numerics;
using Silk.NET.OpenGL;
using Coomer.Features.Capture;
using Coomer.Features.Drawing;
using Coomer.Features.Navigation;
using Coomer.Features.Rendering;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Stickers;

public sealed unsafe class StickerRenderer : IDisposable
{
  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;

  public StickerRenderer(GL gl)
  {
    _gl = gl;
    _shader = new Shader(gl,
        EmbeddedShader.Load("sticker.vert.glsl"),
        EmbeddedShader.Load("sticker.frag.glsl"),
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

  public void DrawStamps(DrawTool tool, Camera camera, bool mirror, Vector2 windowSize,
                         Screenshot shot, Vector2 cursorScreen, StickerCache cache,
                         StickerState state, Coomer.Features.Capture.RegionExporter? exporter,
                         Coomer.Features.Lighting.Flashlight flashlight)
  {
    bool hasSticker = !tool.Hide && tool.StickerStamps.Count > 0;
    bool wantsGhost = tool.IsEnabled && tool.StickerMode && state.Current != null;
    if (!hasSticker && !wantsGhost) return;

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

    _shader.SetInt("flEnabled", flashlight.IsEnabled ? 1 : 0);
    _shader.SetFloat("flShadow", flashlight.Shadow);
    _shader.SetFloat("flRadius", flashlight.Radius);
    _shader.SetVec2("bubblePos", flashlight.Position);
    _shader.SetVec2("bubbleStretch", new Vector2(flashlight.Stretch.X, -flashlight.Stretch.Y));
    _shader.SetFloat("bubbleSqueeze", flashlight.Squeeze);

    _gl.BindVertexArray(_vao);
    _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    _gl.Enable(EnableCap.Blend);
    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    _gl.ActiveTexture(TextureUnit.Texture0);

    foreach (var s in tool.StickerStamps)
    {
      var entry = cache.All.FirstOrDefault(e => e.Path == s.Path);
      if (entry == null) continue;
      DrawOne(entry, s.Center, s.HalfSize, 1.0f, s.MirrorH);
    }

    if (wantsGhost)
    {
      var entry = state.Current!;
      var cursorImg = ScreenToImage(cursorScreen, windowSize, shot, camera, mirror);
      float halfSize = tool.StickerSize * 0.5f;
      DrawOne(entry, cursorImg, halfSize, 0.5f, tool.StickerMirror);
    }

    _gl.Disable(EnableCap.Blend);
  }

  private unsafe void DrawOne(StickerEntry entry, Vector2 centerImg, float halfSize, float opacity, bool mirrorH)
  {
    float aspect = entry.Height == 0 ? 1f : (float)entry.Height / entry.Width;
    float halfW = halfSize;
    float halfH = halfSize * aspect;

    float u0 = mirrorH ? 1f : 0f;
    float u1 = mirrorH ? 0f : 1f;

    Span<float> v = stackalloc float[24]
    {
      centerImg.X - halfW, centerImg.Y - halfH, u0, 0f,
      centerImg.X + halfW, centerImg.Y - halfH, u1, 0f,
      centerImg.X + halfW, centerImg.Y + halfH, u1, 1f,
      centerImg.X - halfW, centerImg.Y - halfH, u0, 0f,
      centerImg.X + halfW, centerImg.Y + halfH, u1, 1f,
      centerImg.X - halfW, centerImg.Y + halfH, u0, 1f,
    };
    _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, v);
    _gl.BindTexture(TextureTarget.Texture2D, entry.Texture);
    _shader.SetFloat("opacity", opacity);
    _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
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
