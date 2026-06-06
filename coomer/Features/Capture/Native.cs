using System.Runtime.InteropServices;

namespace Coomer.Features.Capture;

/// <summary>
/// P/Invokes do Win32 usados na captura. Usa <c>LibraryImport</c> (source-generated),
/// que e a forma compativel com NativeAOT (sem reflection em runtime).
/// </summary>
internal static partial class Native
{
  // ---- DPI awareness ----
  // Handle especial: -4 == DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
  public static readonly nint DpiPerMonitorAwareV2 = -4;

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool SetProcessDpiAwarenessContext(nint value);

  // ---- Monitor sob o cursor ----
  public const uint MONITOR_DEFAULTTONEAREST = 2;

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool GetCursorPos(out Point lpPoint);

  [LibraryImport("user32.dll")]
  public static partial nint MonitorFromPoint(Point pt, uint dwFlags);

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool GetMonitorInfoW(nint hMonitor, ref MonitorInfo lpmi);

  // ---- GDI ----
  public const uint SRCCOPY = 0x00CC0020;
  public const uint DIB_RGB_COLORS = 0;
  public const uint BI_RGB = 0;

  [LibraryImport("user32.dll")]
  public static partial nint GetDC(nint hWnd);

  [LibraryImport("user32.dll")]
  public static partial int ReleaseDC(nint hWnd, nint hDC);

  [LibraryImport("gdi32.dll")]
  public static partial nint CreateCompatibleDC(nint hDC);

  [LibraryImport("gdi32.dll")]
  public static partial nint CreateCompatibleBitmap(nint hDC, int width, int height);

  [LibraryImport("gdi32.dll")]
  public static partial nint SelectObject(nint hDC, nint hObject);

  [LibraryImport("gdi32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool BitBlt(nint hDest, int xDest, int yDest, int width, int height,
                                    nint hSrc, int xSrc, int ySrc, uint rop);

  [LibraryImport("gdi32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool DeleteObject(nint hObject);

  [LibraryImport("gdi32.dll")]
  public static partial int DeleteDC(nint hDC);

  [LibraryImport("gdi32.dll")]
  public static unsafe partial int GetDIBits(nint hDC, nint hBitmap, uint start, uint lines,
                                             byte* bits, ref BitmapInfo info, uint usage);
}

[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
  public int X;
  public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
  public int Left;
  public int Top;
  public int Right;
  public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MonitorInfo
{
  public uint cbSize;
  public Rect rcMonitor;
  public Rect rcWork;
  public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader
{
  public uint biSize;
  public int biWidth;
  public int biHeight;
  public ushort biPlanes;
  public ushort biBitCount;
  public uint biCompression;
  public uint biSizeImage;
  public int biXPelsPerMeter;
  public int biYPelsPerMeter;
  public uint biClrUsed;
  public uint biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfo
{
  public BitmapInfoHeader bmiHeader;
  public uint bmiColors0; // nao usado em 32bpp BI_RGB, mas mantem o layout
}
