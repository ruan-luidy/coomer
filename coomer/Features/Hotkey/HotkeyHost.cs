namespace Coomer.Features.Hotkey;

// Ctrl+Alt+Z abre o overlay; Ctrl+Alt+Q encerra.
public sealed class HotkeyHost
{
  private const int IdShow = 1;
  private const int IdQuit = 2;

  public void Run(Action onShow)
  {
    uint mods = HotkeyNative.MOD_CONTROL | HotkeyNative.MOD_ALT | HotkeyNative.MOD_NOREPEAT;

    // hWnd=0 => WM_HOTKEY chega na fila desta thread.
    if (!HotkeyNative.RegisterHotKey(0, IdShow, mods, HotkeyNative.VK_Z))
      return;
    HotkeyNative.RegisterHotKey(0, IdQuit, mods, HotkeyNative.VK_Q);

    try
    {
      while (HotkeyNative.GetMessage(out Msg msg, 0, 0, 0) > 0)
      {
        if (msg.message != HotkeyNative.WM_HOTKEY)
          continue;

        int id = (int)msg.wParam;
        if (id == IdShow)
          onShow();
        else if (id == IdQuit)
          break;
      }
    }
    finally
    {
      HotkeyNative.UnregisterHotKey(0, IdShow);
      HotkeyNative.UnregisterHotKey(0, IdQuit);
    }
  }
}
