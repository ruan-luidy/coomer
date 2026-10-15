using System.Runtime.InteropServices;

namespace Coomer.Features.Capture;

// Clipboard nativo (sem WinForms/cmd). Owner precisa ser uma janela real:
// com OpenClipboard(0) o EmptyClipboard deixa o dono nulo e o SetClipboardData
// pode falhar. OpenClipboard tambem falha se outro app estiver segurando o
// clipboard naquele instante, dai o retry curto.
internal static partial class Clipboard
{
  public static nint Owner;

  private const uint CF_UNICODETEXT = 13;
  private const uint CF_DIB = 8;
  private const uint GMEM_MOVEABLE = 0x0002;

  public static bool SetDib(byte[] dib) => Set(CF_DIB, dib);

  public static bool SetText(string text)
  {
    var bytes = new byte[(text.Length + 1) * 2]; // UTF-16 + terminador
    System.Text.Encoding.Unicode.GetBytes(text, 0, text.Length, bytes, 0);
    return Set(CF_UNICODETEXT, bytes);
  }

  private static bool Set(uint format, byte[] data)
  {
    if (!TryOpen()) return false;
    try
    {
      EmptyClipboard();
      nint mem = GlobalAlloc(GMEM_MOVEABLE, (nuint)data.Length);
      if (mem == 0) return false;
      nint ptr = GlobalLock(mem);
      if (ptr == 0) { GlobalFree(mem); return false; }
      Marshal.Copy(data, 0, ptr, data.Length);
      GlobalUnlock(mem);
      // sucesso = o sistema vira dono da memoria; falha = ainda e nossa
      if (SetClipboardData(format, mem) != 0) return true;
      GlobalFree(mem);
      return false;
    }
    finally
    {
      CloseClipboard();
    }
  }

  private static bool TryOpen()
  {
    for (int i = 0; i < 10; i++)
    {
      if (OpenClipboard(Owner)) return true;
      Thread.Sleep(10);
    }
    return false;
  }

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool OpenClipboard(nint hWnd);

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool CloseClipboard();

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool EmptyClipboard();

  [LibraryImport("user32.dll")]
  private static partial nint SetClipboardData(uint uFormat, nint hMem);

  [LibraryImport("kernel32.dll")]
  private static partial nint GlobalAlloc(uint uFlags, nuint dwBytes);

  [LibraryImport("kernel32.dll")]
  private static partial nint GlobalLock(nint hMem);

  [LibraryImport("kernel32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool GlobalUnlock(nint hMem);

  [LibraryImport("kernel32.dll")]
  private static partial nint GlobalFree(nint hMem);
}
