using System.Numerics;
using Silk.NET.OpenGL;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Navigation;
using Coomer.Features.Lighting;
using Coomer.Features.Drawing;
using Coomer.Features.Stickers;
using Coomer.Features.Hud;
using Coomer.Features.Text;
using Coomer.Features.Effects;

namespace Coomer.Features.Rendering;

public sealed unsafe class Renderer : IDisposable
{
  private readonly GL _gl;
  private readonly Shader _shader;
  private readonly uint _vao;
  private readonly uint _vbo;
  private readonly uint _ebo;
  private readonly uint _texture;
  private readonly int _imageWidth;
  private readonly int _imageHeight;
  private readonly Screenshot _screenshot;
  private readonly StrokeRenderer _strokes;
  private readonly StickerRenderer _stickerRenderer;
  private readonly TextRenderer _text;
  private readonly HudRenderer _hud;
  private readonly StickerPalette _palette;

  public Renderer(GL gl, Screenshot screenshot)
  {
    _gl = gl;
    _screenshot = screenshot;
    _imageWidth = screenshot.Width;
    _imageHeight = screenshot.Height;

    _shader = new Shader(gl,
        EmbeddedShader.Load("vert.glsl"),
        EmbeddedShader.Load("frag.glsl"),
        new[] { (0u, "aPos"), (1u, "aTexCoord") });

    float w = _imageWidth;
    float h = _imageHeight;
    Span<float> vertices = stackalloc float[]
    {
                w,   0f,   1f,   1f,
                w,   h,    1f,   0f,
                0f,  h,    0f,   0f,
                0f,  0f,   0f,   1f,
        };
    Span<uint> indices = stackalloc uint[] { 0, 1, 3, 1, 2, 3 };

    _vao = gl.GenVertexArray();
    gl.BindVertexArray(_vao);

    _vbo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
    gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);

    _ebo = gl.GenBuffer();
    gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
    gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);

    const uint stride = 4 * sizeof(float);
    gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
    gl.EnableVertexAttribArray(0);
    gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
    gl.EnableVertexAttribArray(1);

    _texture = gl.GenTexture();
    gl.ActiveTexture(TextureUnit.Texture0);
    gl.BindTexture(TextureTarget.Texture2D, _texture);

    fixed (byte* data = screenshot.Pixels)
    {
      gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgb,
          (uint)_imageWidth, (uint)_imageHeight, 0,
          PixelFormat.Bgra, PixelType.UnsignedByte, data);
    }
    gl.GenerateMipmap(TextureTarget.Texture2D);

    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToBorder);
    gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToBorder);

    _shader.Use();
    _shader.SetInt("tex", 0);

    _strokes = new StrokeRenderer(gl);
    _stickerRenderer = new StickerRenderer(gl);
    _text = new TextRenderer(gl);
    _hud = new HudRenderer(_text);
    _palette = new StickerPalette(gl, _text);
  }

  public void Draw(Camera camera, Flashlight flashlight, Config config,
                   bool mirror, Vector2 windowSize, Vector2 cursor,
                   DrawTool drawTool, ColorHistory? history, RegionExporter? exporter,
                   StickerCache stickers, StickerState stickerState,
                   ColorPicker picker, RippleEffect ripple)
  {
    _text.NewFrame();
    _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
    _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

    _gl.ActiveTexture(TextureUnit.Texture0);
    _gl.BindTexture(TextureTarget.Texture2D, _texture);

    _shader.Use();
    _shader.SetVec2("cameraPos", camera.Position);
    _shader.SetFloat("cameraScale", camera.Scale);
    _shader.SetVec2("screenshotSize", new Vector2(_imageWidth, _imageHeight));
    _shader.SetVec2("windowSize", windowSize);
    _shader.SetVec2("cursorPos", cursor);
    _shader.SetFloat("flShadow", flashlight.Shadow);
    _shader.SetFloat("flRadius", flashlight.Radius);
    _shader.SetInt("mirror", mirror ? 1 : 0);

    _shader.SetVec2("bubblePos", flashlight.Position);
    _shader.SetVec2("bubbleStretch", new Vector2(flashlight.Stretch.X, -flashlight.Stretch.Y));
    _shader.SetFloat("bubbleSqueeze", flashlight.Squeeze);
    _shader.SetInt("flEnabled", flashlight.IsEnabled ? 1 : 0);

    _shader.SetInt("blurBackground", config.BlurBackground ? 1 : 0);
    _shader.SetFloat("backgroundBlurRadius", config.BackgroundBlurRadius);
    _shader.SetInt("blurOutsideFl", config.BlurOutsideFlashlight ? 1 : 0);
    _shader.SetFloat("outsideFlBlurRadius", config.OutsideFlashlightBlurRadius);

    _shader.SetInt("flFisheye", config.FlashlightFisheye ? 1 : 0);
    _shader.SetFloat("fisheyeStrength", config.FisheyeStrength);
    _shader.SetInt("flClearGlass", config.FlashlightClearGlass ? 1 : 0);
    _shader.SetFloat("clearGlassZoom", config.ClearGlassZoom);

    bool invertActive = exporter != null && exporter.Dragging;
    _shader.SetInt("invertRect", invertActive ? 1 : 0);
    _shader.SetInt("invertMode", config.PrintEffect == "glass" ? 1 : 0);
    if (invertActive)
    {
      var a = exporter!.Start;
      var b = exporter.End;
      _shader.SetVec2("invertMin", new Vector2(MathF.Min(a.X, b.X), MathF.Min(a.Y, b.Y)));
      _shader.SetVec2("invertMax", new Vector2(MathF.Max(a.X, b.X), MathF.Max(a.Y, b.Y)));
    }

    _gl.BindVertexArray(_vao);
    _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

    int invertMode = config.PrintEffect == "glass" ? 1 : 0;
    _stickerRenderer.DrawStamps(drawTool, camera, mirror, windowSize, _screenshot, cursor, stickers, stickerState, exporter, flashlight, _text, invertMode);
    _strokes.Draw(drawTool, camera, mirror, windowSize, _screenshot, cursor, history, exporter, invertMode);
    _palette.Draw(windowSize, drawTool, stickers, stickerState);
    _hud.Draw(windowSize, drawTool, picker, exporter!, stickerState);
  }

  public void Dispose()
  {
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteBuffer(_vbo);
    _gl.DeleteBuffer(_ebo);
    _gl.DeleteTexture(_texture);
    _shader.Dispose();
    _strokes.Dispose();
    _stickerRenderer.Dispose();
    _palette.Dispose();
    _text.Dispose();
  }
}
