using System.Runtime.InteropServices;

namespace Coomer.Features.Hotkey;

/// <summary>
/// P/Invokes do Win32 para atalhos globais (RegisterHotKey) e o loop de mensagens
/// que mantem o processo vivo esperando o atalho.
/// </summary>
internal static partial class HotkeyNative
{
  public const uint MOD_ALT = 0x0001;
  public const uint MOD_CONTROL = 0x0002;
  public const uint MOD_SHIFT = 0x0004;
  public const uint MOD_NOREPEAT = 0x4000;
  public const uint WM_HOTKEY = 0x0312;

  // Virtual-Key codes
  public const uint VK_Z = 0x5A;
  public const uint VK_Q = 0x51;

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

  [LibraryImport("user32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool UnregisterHotKey(nint hWnd, int id);

  // GetMessage bloqueia ate chegar uma mensagem (0% CPU enquanto espera o atalho).
  [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
  public static partial int GetMessage(out Msg lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
}

[StructLayout(LayoutKind.Sequential)]
internal struct Msg
{
  public nint hwnd;
  public uint message;
  public nuint wParam;
  public nint lParam;
  public uint time;
  public int ptX;
  public int ptY;
}
