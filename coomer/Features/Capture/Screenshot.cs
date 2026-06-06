namespace Coomer.Features.Capture;

/// <summary>
/// Porte de <c>screenshot.nim</c>. Captura UMA vez (modo padrao do boomer) via GDI
/// <c>BitBlt</c> e devolve os pixels em BGRA, top-down — formato que casa direto com o
/// <c>GL_BGRA</c> usado no upload da textura. Guarda tambem a origem (OriginX/Y) do
/// monitor capturado, para a janela abrir exatamente em cima dele.
/// </summary>
public sealed unsafe class Screenshot
{
  public int Width { get; }
  public int Height { get; }
  public int OriginX { get; }
  public int OriginY { get; }
  public byte[] Pixels { get; }

  private Screenshot(int x, int y, int width, int height, byte[] pixels)
  {
    OriginX = x;
    OriginY = y;
    Width = width;
    Height = height;
    Pixels = pixels;
  }

  /// <summary>Liga DPI awareness. DEVE ser chamado antes de qualquer janela/captura.</summary>
  public static void EnableDpiAwareness()
      => Native.SetProcessDpiAwarenessContext(Native.DpiPerMonitorAwareV2);

  /// <summary>Captura apenas o monitor onde o cursor esta (evita juntar os 2 monitores).</summary>
  public static Screenshot CaptureMonitorUnderCursor()
  {
    Native.GetCursorPos(out Point cursor);
    nint monitor = Native.MonitorFromPoint(cursor, Native.MONITOR_DEFAULTTONEAREST);

    var mi = new MonitorInfo { cbSize = (uint)sizeof(MonitorInfo) };
    Native.GetMonitorInfoW(monitor, ref mi);

    int x = mi.rcMonitor.Left;
    int y = mi.rcMonitor.Top;
    int w = mi.rcMonitor.Right - mi.rcMonitor.Left;
    int h = mi.rcMonitor.Bottom - mi.rcMonitor.Top;

    return Grab(x, y, w, h);
  }

  private static Screenshot Grab(int x, int y, int w, int h)
  {
    nint screenDc = Native.GetDC(0);
    nint memDc = Native.CreateCompatibleDC(screenDc);
    nint bitmap = Native.CreateCompatibleBitmap(screenDc, w, h);
    nint previous = Native.SelectObject(memDc, bitmap);

    Native.BitBlt(memDc, 0, 0, w, h, screenDc, x, y, Native.SRCCOPY);
    Native.SelectObject(memDc, previous); // desselecionar antes do GetDIBits

    var pixels = new byte[w * h * 4];
    var info = new BitmapInfo
    {
      bmiHeader = new BitmapInfoHeader
      {
        biSize = (uint)sizeof(BitmapInfoHeader),
        biWidth = w,
        biHeight = -h, // negativo = top-down (linha 0 = topo), igual ao XGetImage
        biPlanes = 1,
        biBitCount = 32,
        biCompression = Native.BI_RGB,
      }
    };

    fixed (byte* p = pixels)
      Native.GetDIBits(memDc, bitmap, 0, (uint)h, p, ref info, Native.DIB_RGB_COLORS);

    Native.DeleteObject(bitmap);
    Native.DeleteDC(memDc);
    Native.ReleaseDC(0, screenDc);

    return new Screenshot(x, y, w, h, pixels);
  }
}
