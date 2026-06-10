using System.Runtime.InteropServices;

namespace Coomer.App;

internal static partial class OverlayWindowNative
{
  private const int GWL_EXSTYLE = -20;
  private const long WS_EX_TOOLWINDOW = 0x00000080;
  private const long WS_EX_APPWINDOW = 0x00040000;

  [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
  private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

  [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
  private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool SetForegroundWindow(nint hWnd);

  // Tool window: some da barra de tarefas e do Alt+Tab.
  public static void HideFromTaskbar(nint hWnd)
  {
    if (hWnd == 0) return;
    long ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
    ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
    SetWindowLongPtr(hWnd, GWL_EXSTYLE, (nint)ex);
  }

  [LibraryImport("user32.dll")]
  public static partial int ShowCursor([MarshalAs(UnmanagedType.Bool)] bool bShow);

  private static bool _hidden;

  public static void SetCursorVisible(bool visible)
  {
    if (visible && _hidden)
    {
      while (ShowCursor(true) < 0) { }
      _hidden = false;
    }
    else if (!visible && !_hidden)
    {
      while (ShowCursor(false) >= 0) { }
      _hidden = true;
    }
  }
}
