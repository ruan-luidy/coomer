using System.Runtime.InteropServices;

namespace Coomer.App;

internal static partial class OverlayWindowNative
{
  private const int GWL_EXSTYLE = -20;
  private const long WS_EX_TOOLWINDOW  = 0x00000080;
  private const long WS_EX_APPWINDOW   = 0x00040000;
  private const long WS_EX_LAYERED     = 0x00080000;
  private const long WS_EX_TRANSPARENT = 0x00000020;

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

  // Click-through: WS_EX_TRANSPARENT faz o hit-test do mouse pular o overlay,
  // e mouse vai direto pro que ta atras. WS_EX_LAYERED e obrigatorio pra
  // TRANSPARENT funcionar mesmo sem alpha real (a textura continua opaca por
  // baixo). O teclado nao e afetado por isso — a janela perde foco quando o
  // usuario clica em outra coisa, por isso o hook de Esc.
  public static void SetClickThrough(nint hWnd, bool on)
  {
    if (hWnd == 0) return;
    long ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
    if (on) ex |= WS_EX_LAYERED | WS_EX_TRANSPARENT;
    else    ex &= ~(WS_EX_LAYERED | WS_EX_TRANSPARENT);
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

  // -------------------------- low-level keyboard hook ---------------------
  // Em passthrough o overlay nao tem foco — o usuario ta mexendo no PC, e
  // teclas digitadas vao pros outros programas. Pra ouvir Esc sem roubar a
  // tecla de quem ta atras instalamos um WH_KEYBOARD_LL (system-wide). O
  // callback roda na thread que instalou o hook (a do overlay), entao da pra
  // empurrar callbacks safe-thread.
  //
  // Chamamos CallNextHookEx sem retornar 1: assim o Esc continua chegando
  // normalmente em quem tiver foco (nao queremos engolir a tecla do usuario).

  private const int WH_KEYBOARD_LL = 13;
  private const int WM_KEYDOWN = 0x0100;
  private const int WM_SYSKEYDOWN = 0x0104;
  private const int VK_ESCAPE = 0x1B;

  [StructLayout(LayoutKind.Sequential)]
  private struct KbdLlHookStruct
  {
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public nuint dwExtraInfo;
  }

  private delegate nint HookProc(int nCode, nuint wParam, nint lParam);

  [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW")]
  private static partial nint SetWindowsHookEx(int idHook, nint lpfn, nint hMod, uint dwThreadId);

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static partial bool UnhookWindowsHookEx(nint hhk);

  [LibraryImport("user32.dll")]
  private static partial nint CallNextHookEx(nint hhk, int nCode, nuint wParam, nint lParam);

  [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
  private static partial nint GetModuleHandle(string? lpModuleName);

  private static nint _hookHandle;
  private static HookProc? _hookDelegate; // GC-pin
  private static Action? _onEscape;

  public static void InstallEscapeHook(Action onEscape)
  {
    if (_hookHandle != 0) return;
    _onEscape = onEscape;
    _hookDelegate = LowLevelKeyboardProc;
    nint fn = Marshal.GetFunctionPointerForDelegate(_hookDelegate);
    _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, fn, GetModuleHandle(null), 0);
  }

  public static void UninstallEscapeHook()
  {
    if (_hookHandle == 0) return;
    UnhookWindowsHookEx(_hookHandle);
    _hookHandle = 0;
    _hookDelegate = null;
    _onEscape = null;
  }

  private static nint LowLevelKeyboardProc(int nCode, nuint wParam, nint lParam)
  {
    if (nCode == 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
    {
      var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
      if (data.vkCode == VK_ESCAPE) _onEscape?.Invoke();
    }
    return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
  }
}
