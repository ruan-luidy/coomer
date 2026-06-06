namespace Coomer.Features.Hotkey;

/// <summary>
/// Mantem o processo vivo em segundo plano e escuta atalhos globais:
///   Ctrl+Alt+Z -> dispara o overlay (callback onShow)
///   Ctrl+Alt+Q -> encerra o coomer
/// Como o runtime ja esta quente, o overlay aparece quase instantaneo.
/// </summary>
public sealed class HotkeyHost
{
  private const int IdShow = 1;
  private const int IdQuit = 2;

  public void Run(Action onShow)
  {
    uint mods = HotkeyNative.MOD_CONTROL | HotkeyNative.MOD_ALT | HotkeyNative.MOD_NOREPEAT;

    // hWnd = 0 => o WM_HOTKEY chega na fila de mensagens DESTA thread.
    if (!HotkeyNative.RegisterHotKey(0, IdShow, mods, HotkeyNative.VK_Z))
      return; // Ctrl+Alt+Z ja em uso por outro app; sem o atalho o coomer nao serve, entao sai.
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
