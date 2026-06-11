using System.Numerics;
using Silk.NET.OpenGL;
using Coomer.Features.Capture;
using Coomer.Features.Drawing;
using Coomer.Features.Navigation;
using Coomer.Features.Text;
using Coomer.Features.Rendering;
using Shader = Coomer.Features.Rendering.Shader;

namespace Coomer.Features.Stickers;

public sealed unsafe class StickerRenderer : IDisposable
{
  private static readonly Vector4 OutlineCyan = new(0.30f, 0.85f, 1.00f, 1.00f);
  private static readonly Vector4 NoTint = new(1f, 1f, 1f, 1f);

  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;
  private readonly uint _whiteTex;

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

    _whiteTex = gl.GenTexture();
    gl.BindTexture(TextureTarget.Texture2D, _whiteTex);
    Span<byte> white = stackalloc byte[] { 255, 255, 255, 255 };
    fixed (byte* p = white)
      gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8, 1u, 1u, 0,
          Silk.NET.OpenGL.PixelFormat.Rgba, PixelType.UnsignedByte, p);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);

    _shader.Use();
    _shader.SetInt("tex", 0);
  }

  public void DrawStamps(DrawTool tool, Camera camera, bool mirror, Vector2 windowSize,
                         Screenshot shot, Vector2 cursorScreen, StickerCache cache,
                         StickerState state, Coomer.Features.Capture.RegionExporter? exporter,
                         Coomer.Features.Lighting.Flashlight flashlight,
                         Coomer.Features.Text.TextRenderer textRenderer,
                         int invertMode)
  {
    bool hasSticker = !tool.Hide && tool.StickerStamps.Count > 0;
    bool wantsGhost = tool.IsEnabled && tool.StickerMode && state.Current != null && tool.SelectedStickerIndex < 0;
    bool wantsOutline = !tool.Hide && tool.SelectedStickerIndex >= 0 && tool.SelectedStickerIndex < tool.StickerStamps.Count;
    bool hasText = !tool.Hide && (tool.TextStamps.Count > 0 || tool.ActiveText != null);
    if (!hasSticker && !wantsGhost && !wantsOutline && !hasText) return;

    var screenshotSize = new Vector2(shot.Width, shot.Height);

    _shader.Use();
    _shader.SetVec2("cameraPos", camera.Position);
    _shader.SetFloat("cameraScale", camera.Scale);
    _shader.SetVec2("windowSize", windowSize);
    _shader.SetVec2("screenshotSize", screenshotSize);
    _shader.SetInt("mirror", mirror ? 1 : 0);

    bool invertActive = exporter != null && exporter.Dragging;
    _shader.SetInt("invertRect", invertActive ? 1 : 0);
    _shader.SetInt("invertMode", invertMode);
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
    _shader.SetVec4("tint", NoTint);

    foreach (var s in tool.StickerStamps)
    {
      var entry = cache.All.FirstOrDefault(e => e.Path == s.Path);
      if (entry == null) continue;
      DrawOne(entry.Texture, s.Center, s.HalfSize, s.HalfSize * Aspect(entry), 1.0f, s.MirrorH, s.Rotation);
    }

    if (wantsGhost)
    {
      var entry = state.Current!;
      var cursorImg = ScreenToImage(cursorScreen, windowSize, shot, camera, mirror);
      float halfSize = tool.StickerSize * 0.5f;
      DrawOne(entry.Texture, cursorImg, halfSize, halfSize * Aspect(entry), 0.5f, tool.StickerMirror, 0f);
    }

    if (wantsOutline)
    {
      var sel = tool.StickerStamps[tool.SelectedStickerIndex];
      var entry = cache.All.FirstOrDefault(e => e.Path == sel.Path);
      if (entry != null)
        DrawOutline(sel.Center, sel.HalfSize, sel.HalfSize * Aspect(entry), sel.Rotation, camera.Scale);
    }

    if (hasText)
    {
      foreach (var t in tool.TextStamps)
        DrawTextStamp(textRenderer, t, 1.0f);
      if (tool.ActiveText != null && tool.ActiveText.Text.Length > 0)
        DrawTextStamp(textRenderer, tool.ActiveText, 1.0f);
      if (tool.ActiveText != null) DrawTextCaret(tool.ActiveText, camera.Scale);
    }

    _shader.SetVec4("tint", NoTint);
    _gl.Disable(EnableCap.Blend);
  }

  private unsafe void DrawTextStamp(Coomer.Features.Text.TextRenderer tr, TextStamp t, float opacity)
  {
    if (string.IsNullOrEmpty(t.Text)) return;
    var (tex, tw, th) = tr.GetTexture(t.Text, t.FontSizePx);
    _shader.SetVec4("tint", t.Color);
    DrawOne(tex, t.TopLeft + new Vector2(tw, th) * 0.5f, tw * 0.5f, th * 0.5f, opacity, false, 0f);
  }

  // Caret simples piscando perto da posicao do prox char (top-right do texto atual).
  private unsafe void DrawTextCaret(TextStamp t, float cameraScale)
  {
    float caretW = MathF.Max(1.5f / cameraScale, 0.5f);
    float caretH = t.FontSizePx * 0.5f;
    Vector2 origin;
    if (string.IsNullOrEmpty(t.Text))
      origin = t.TopLeft + new Vector2(0, caretH * 0.5f);
    else
    {
      // posiciona o caret no fim do texto — sem TextRenderer aqui, aproxima
      // pegando o quad ja desenhado (usamos um lookup pelo GetTexture).
      // Pra v1 usamos a borda direita do TopLeft + um deslocamento simples.
      // O caret apenas indica "estou digitando aqui".
      origin = t.TopLeft + new Vector2(0, caretH * 0.5f); // mesmo lugar; visual ok pra MVP
    }
    _gl.BindTexture(TextureTarget.Texture2D, _whiteTex);
    _shader.SetFloat("opacity", 1.0f);
    // pisca: usa tempo via ticks (~2 Hz). simples e nao precisa estado.
    bool on = (Environment.TickCount / 500) % 2 == 0;
    if (!on) return;
    _shader.SetVec4("tint", t.Color);
    var p0 = origin;
    var p1 = origin + new Vector2(caretW * 2f, 0);
    var p2 = origin + new Vector2(caretW * 2f, caretH);
    var p3 = origin + new Vector2(0, caretH);
    Span<float> v = stackalloc float[24]
    {
      p0.X, p0.Y, 0f, 0f,
      p1.X, p1.Y, 1f, 0f,
      p2.X, p2.Y, 1f, 1f,
      p0.X, p0.Y, 0f, 0f,
      p2.X, p2.Y, 1f, 1f,
      p3.X, p3.Y, 0f, 1f,
    };
    _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, v);
    _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
  }

  private static float Aspect(StickerEntry e) => e.Height == 0 ? 1f : (float)e.Height / e.Width;

  private unsafe void DrawOne(uint texture, Vector2 centerImg, float halfW, float halfH,
                              float opacity, bool mirrorH, float rotation)
  {
    float u0 = mirrorH ? 1f : 0f;
    float u1 = mirrorH ? 0f : 1f;
    float cos = MathF.Cos(rotation);
    float sin = MathF.Sin(rotation);
    Vector2 Rot(float lx, float ly) => new(centerImg.X + lx * cos - ly * sin, centerImg.Y + lx * sin + ly * cos);

    var tl = Rot(-halfW, -halfH);
    var tr = Rot( halfW, -halfH);
    var br = Rot( halfW,  halfH);
    var bl = Rot(-halfW,  halfH);

    Span<float> v = stackalloc float[24]
    {
      tl.X, tl.Y, u0, 0f,
      tr.X, tr.Y, u1, 0f,
      br.X, br.Y, u1, 1f,
      tl.X, tl.Y, u0, 0f,
      br.X, br.Y, u1, 1f,
      bl.X, bl.Y, u0, 1f,
    };
    _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, v);
    _gl.BindTexture(TextureTarget.Texture2D, texture);
    _shader.SetFloat("opacity", opacity);
    _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);
  }

  // Outline: 4 quads finos cyan, extrudidos pra fora, seguindo a rotacao do sticker.
  private unsafe void DrawOutline(Vector2 centerImg, float halfW, float halfH, float rotation, float cameraScale)
  {
    float thicknessImg = MathF.Max(1.5f / cameraScale, 0.5f);
    float cos = MathF.Cos(rotation);
    float sin = MathF.Sin(rotation);
    Vector2 Rot(float lx, float ly) => new(centerImg.X + lx * cos - ly * sin, centerImg.Y + lx * sin + ly * cos);

    _gl.BindTexture(TextureTarget.Texture2D, _whiteTex);
    _shader.SetFloat("opacity", 1.0f);
    _shader.SetVec4("tint", OutlineCyan);

    // top
    EmitEdgeQuad(Rot(-halfW - thicknessImg, -halfH - thicknessImg),
                 Rot( halfW + thicknessImg, -halfH - thicknessImg),
                 Rot( halfW + thicknessImg, -halfH + thicknessImg),
                 Rot(-halfW - thicknessImg, -halfH + thicknessImg));
    // bottom
    EmitEdgeQuad(Rot(-halfW - thicknessImg,  halfH - thicknessImg),
                 Rot( halfW + thicknessImg,  halfH - thicknessImg),
                 Rot( halfW + thicknessImg,  halfH + thicknessImg),
                 Rot(-halfW - thicknessImg,  halfH + thicknessImg));
    // left
    EmitEdgeQuad(Rot(-halfW - thicknessImg, -halfH + thicknessImg),
                 Rot(-halfW + thicknessImg, -halfH + thicknessImg),
                 Rot(-halfW + thicknessImg,  halfH - thicknessImg),
                 Rot(-halfW - thicknessImg,  halfH - thicknessImg));
    // right
    EmitEdgeQuad(Rot( halfW - thicknessImg, -halfH + thicknessImg),
                 Rot( halfW + thicknessImg, -halfH + thicknessImg),
                 Rot( halfW + thicknessImg,  halfH - thicknessImg),
                 Rot( halfW - thicknessImg,  halfH - thicknessImg));
  }

  private unsafe void EmitEdgeQuad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
  {
    Span<float> v = stackalloc float[24]
    {
      p0.X, p0.Y, 0f, 0f,
      p1.X, p1.Y, 1f, 0f,
      p2.X, p2.Y, 1f, 1f,
      p0.X, p0.Y, 0f, 0f,
      p2.X, p2.Y, 1f, 1f,
      p3.X, p3.Y, 0f, 1f,
    };
    _gl.BufferSubData<float>(BufferTargetARB.ArrayBuffer, 0, v);
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
    _gl.DeleteTexture(_whiteTex);
    _shader.Dispose();
  }
}
