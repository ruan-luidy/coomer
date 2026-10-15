using System.Numerics;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Coomer.Features.Capture;

// Region copy (CF_DIB no clipboard) e save full (PNG em Pictures/). Le pixels
// via glReadPixels — o CoomerApp chama FlushAfterRender depois do Draw, antes
// do swap.
public sealed class RegionExporter
{
  public bool CopyMode { get; private set; }
  public bool Dragging { get; private set; }
  public Vector2 Start { get; private set; }
  public Vector2 End { get; private set; }

  public bool Active => CopyMode;

  private bool _pendingCopy;
  private bool _pendingSaveFull;
  private Vector2 _pendingA;
  private Vector2 _pendingB;

  public string? LastStatus;
  public float StatusTtl;
  // status vindo da thread do save (encode de PNG nao trava o frame)
  private volatile string? _asyncStatus;

  public void ToggleCopy()
  {
    CopyMode = !CopyMode;
    Dragging = false;
  }

  public void Cancel()
  {
    CopyMode = false;
    Dragging = false;
  }

  public void RequestSaveFull() => _pendingSaveFull = true;

  public void BeginDrag(Vector2 screen)
  {
    if (!CopyMode) return;
    Start = End = screen;
    Dragging = true;
  }

  public void Move(Vector2 screen)
  {
    if (!Dragging) return;
    End = screen;
  }

  public void Finish()
  {
    if (!Dragging) return;
    Dragging = false;
    _pendingA = Start;
    _pendingB = End;
    _pendingCopy = true;
    // CopyMode segue true ate FlushAfterRender consumir o capture, pra
    // bolha congelada nao snapar pro cursor antes do readback.
  }

  public void TickStatus(float dt)
  {
    if (_asyncStatus is { } s)
    {
      _asyncStatus = null;
      SetStatus(s);
    }
    if (StatusTtl > 0f)
    {
      StatusTtl -= dt;
      if (StatusTtl <= 0f) LastStatus = null;
    }
  }

  public unsafe void FlushAfterRender(GL gl, int viewportYOffset, int screenWidth, int screenHeight)
  {
    if (_pendingCopy)
    {
      _pendingCopy = false;
      CopyMode = false;
      var rect = ComputeScreenRect(_pendingA, _pendingB, screenWidth, screenHeight);
      if (rect.w >= 4 && rect.h >= 4)
      {
        var pixels = ReadPixelsBgra(gl, rect.x, rect.y, rect.w, rect.h,
                                    viewportYOffset, screenHeight);
        var dib = EncodeDib(pixels, rect.w, rect.h);
        if (Clipboard.SetDib(dib))
          SetStatus($"copiado: {rect.w}x{rect.h}");
        else
          SetStatus("clipboard falhou");
      }
      else
      {
        SetStatus("regiao pequena demais");
      }
    }

    if (_pendingSaveFull)
    {
      _pendingSaveFull = false;
      var pixels = ReadPixelsBgra(gl, 0, 0, screenWidth, screenHeight,
                                  viewportYOffset, screenHeight);
      SetStatus("salvando...");
      Task.Run(() =>
      {
        var path = SavePng(pixels, screenWidth, screenHeight);
        _asyncStatus = path == null ? "save falhou" : $"salvo: {Path.GetFileName(path)}";
      });
    }
  }

  public void SetStatus(string s)
  {
    LastStatus = s;
    StatusTtl = 2.5f;
  }

  private static (int x, int y, int w, int h) ComputeScreenRect(Vector2 a, Vector2 b, int sw, int sh)
  {
    int x0 = (int)MathF.Min(a.X, b.X);
    int y0 = (int)MathF.Min(a.Y, b.Y);
    int x1 = (int)MathF.Max(a.X, b.X);
    int y1 = (int)MathF.Max(a.Y, b.Y);
    x0 = Math.Clamp(x0, 0, sw - 1);
    y0 = Math.Clamp(y0, 0, sh - 1);
    x1 = Math.Clamp(x1, 0, sw);
    y1 = Math.Clamp(y1, 0, sh);
    return (x0, y0, x1 - x0, y1 - y0);
  }

  // GL devolve linhas bottom-up; reorganizamos pra top-down. O alpha do
  // framebuffer nao e opacidade de verdade (o blend dos tracos mexe nele),
  // entao forca 255 — senao o traco sai semi-transparente em app que
  // respeita alpha ao colar/abrir.
  private static unsafe byte[] ReadPixelsBgra(GL gl, int sx, int sy, int w, int h,
                                              int viewportYOffset, int screenHeight)
  {
    int fbY = viewportYOffset + (screenHeight - sy - h);
    var raw = new byte[w * h * 4];
    fixed (byte* p = raw)
    {
      gl.ReadPixels(sx, fbY, (uint)w, (uint)h, PixelFormat.Bgra, PixelType.UnsignedByte, p);
    }
    var flipped = new byte[raw.Length];
    int stride = w * 4;
    for (int row = 0; row < h; row++)
      System.Buffer.BlockCopy(raw, (h - 1 - row) * stride, flipped, row * stride, stride);
    for (int i = 3; i < flipped.Length; i += 4)
      flipped[i] = 255;
    return flipped;
  }

  // CF_DIB = BITMAPINFOHEADER + pixels bottom-up.
  private static byte[] EncodeDib(byte[] topDownBgra, int w, int h)
  {
    int stride = w * 4;
    int pixelBytes = stride * h;
    int infoHeaderSize = 40;
    var dib = new byte[infoHeaderSize + pixelBytes];
    int o = 0;

    WriteI32(dib, o, infoHeaderSize); o += 4;
    WriteI32(dib, o, w); o += 4;
    WriteI32(dib, o, h); o += 4;
    WriteI16(dib, o, 1); o += 2;
    WriteI16(dib, o, 32); o += 2;
    WriteI32(dib, o, 0); o += 4;
    WriteI32(dib, o, pixelBytes); o += 4;
    WriteI32(dib, o, 2835); o += 4;
    WriteI32(dib, o, 2835); o += 4;
    WriteI32(dib, o, 0); o += 4;
    WriteI32(dib, o, 0); o += 4;

    for (int row = 0; row < h; row++)
      System.Buffer.BlockCopy(topDownBgra, (h - 1 - row) * stride, dib, o + row * stride, stride);

    return dib;
  }

  private static void WriteI32(byte[] b, int o, int v)
  {
    b[o + 0] = (byte)(v & 0xFF);
    b[o + 1] = (byte)((v >> 8) & 0xFF);
    b[o + 2] = (byte)((v >> 16) & 0xFF);
    b[o + 3] = (byte)((v >> 24) & 0xFF);
  }

  private static void WriteI16(byte[] b, int o, int v)
  {
    b[o + 0] = (byte)(v & 0xFF);
    b[o + 1] = (byte)((v >> 8) & 0xFF);
  }

  private static string? SavePng(byte[] topDownBgra, int w, int h)
  {
    try
    {
      var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
      Directory.CreateDirectory(dir);
      var path = Path.Combine(dir, $"coomer_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
      using var image = Image.LoadPixelData<Bgra32>(topDownBgra, w, h);
      image.SaveAsPng(path);
      return path;
    }
    catch (Exception ex)
    {
      Log.Error("save png", ex);
      return null;
    }
  }
}
