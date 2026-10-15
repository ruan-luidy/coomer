namespace Coomer.Features.Hotkey;

// Ctrl+Alt+Z abre o overlay; Ctrl+Alt+Q encerra.
public sealed class HotkeyHost
{
  private const int IdShow = 1;
  private const int IdQuit = 2;

  // false = nao deu pra registrar o atalho de abrir (outro app ja usa).
  public bool Run(Action onShow)
  {
    uint mods = HotkeyNative.MOD_CONTROL | HotkeyNative.MOD_ALT | HotkeyNative.MOD_NOREPEAT;

    // hWnd=0 => WM_HOTKEY chega na fila desta thread. Antes uma falha aqui
    // fazia o processo sair calado e parecia que o coomer nem abria.
    if (!HotkeyNative.RegisterHotKey(0, IdShow, mods, HotkeyNative.VK_Z))
    {
      Log.Error("Ctrl+Alt+Z ja registrado por outro programa");
      HotkeyNative.MessageBox(0,
          "Ctrl+Alt+Z ja esta sendo usado por outro programa, entao o coomer nao tem como abrir.\n\n"
        + "Feche o programa que usa esse atalho e rode o coomer de novo.",
          "coomer", HotkeyNative.MB_ICONWARNING);
      return false;
    }
    if (!HotkeyNative.RegisterHotKey(0, IdQuit, mods, HotkeyNative.VK_Q))
      Log.Error("Ctrl+Alt+Q ja registrado por outro programa; sair so pelo gerenciador de tarefas");

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
    return true;
  }
}
