using System.Numerics;
using Silk.NET.OpenGL;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Navigation;
using Coomer.Features.Lighting;
using Coomer.Features.Drawing;

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
  }

  public void Draw(Camera camera, Flashlight flashlight, Config config,
                   bool mirror, Vector2 windowSize, Vector2 cursor,
                   DrawTool drawTool, ColorHistory? history, RegionExporter? exporter)
  {
    _gl.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
    _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

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

    _gl.BindVertexArray(_vao);
    _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

    _strokes.Draw(drawTool, camera, mirror, windowSize, _screenshot, cursor, history, exporter);
  }

  public void Dispose()
  {
    _gl.DeleteVertexArray(_vao);
    _gl.DeleteBuffer(_vbo);
    _gl.DeleteBuffer(_ebo);
    _gl.DeleteTexture(_texture);
    _shader.Dispose();
    _strokes.Dispose();
  }
}
