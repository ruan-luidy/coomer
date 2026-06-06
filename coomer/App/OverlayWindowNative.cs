using System.Runtime.InteropServices;

namespace Coomer.App;

/// <summary>
/// Ajustes Win32 na janela do overlay: tira da barra de tarefas / Alt+Tab
/// (marcando como tool window) e traz pra frente com foco.
/// </summary>
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

  /// <summary>Marca a janela como tool window: some da barra de tarefas e do Alt+Tab.</summary>
  public static void HideFromTaskbar(nint hWnd)
  {
    if (hWnd == 0) return;
    long ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
    ex = (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
    SetWindowLongPtr(hWnd, GWL_EXSTYLE, (nint)ex);
  }
}
