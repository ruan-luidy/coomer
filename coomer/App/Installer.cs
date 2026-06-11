using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Coomer.App;

// CLI do coomer: --install / --uninstall / --version / --help.
// Autostart via chave Run do HKCU (sem COM, AOT-safe) apontando pro exe atual —
// funciona tanto pra instalacao manual quanto pro shim do Scoop. Mesmo efeito do
// atalho na pasta Startup, so que mais robusto.
internal static class Installer
{
  private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
  private const string AppName = "coomer";

  // Resultado: true = era flag de CLI (ja tratada, nao abrir overlay).
  public static bool TryHandle(string[] args, out int exitCode)
  {
    exitCode = 0;
    if (args.Length == 0) return false;

    string cmd = args[0].TrimStart('-', '/').ToLowerInvariant();
    switch (cmd)
    {
      case "install": exitCode = Install(); return true;
      case "uninstall": exitCode = Uninstall(); return true;
      case "version" or "v": WithConsole(() => Console.WriteLine(Version)); return true;
      case "help" or "h" or "?": WithConsole(PrintUsage); return true;
      default:
        WithConsole(() => { Console.Error.WriteLine($"coomer: comando desconhecido '{args[0]}'"); PrintUsage(); });
        exitCode = 2;
        return true;
    }
  }

  public static string Version =>
    Assembly.GetExecutingAssembly().GetName().Version is { } v
      ? $"{v.Major}.{v.Minor}.{v.Build}"
      : "0.0.0";

  private static int Install()
  {
    int code = 0;
    WithConsole(() =>
    {
      string? exe = Environment.ProcessPath;
      if (exe is null) { Console.Error.WriteLine("coomer: nao consegui resolver o caminho do exe."); code = 1; return; }

      using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
      key.SetValue(AppName, $"\"{exe}\"");
      RemoveLegacyStartupShortcut();

      Console.WriteLine($"coomer instalado pra iniciar com o Windows.");
      Console.WriteLine($"  exe: {exe}");
      Console.WriteLine($"Abra o overlay com Ctrl+Alt+Z. Pra desfazer: coomer --uninstall");
    });
    return code;
  }

  private static int Uninstall()
  {
    WithConsole(() =>
    {
      using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
      key?.DeleteValue(AppName, throwOnMissingValue: false);
      RemoveLegacyStartupShortcut();
      Console.WriteLine("coomer nao vai mais iniciar com o Windows.");
    });
    return 0;
  }

  // Limpa o atalho .lnk antigo (instalacoes manuais anteriores) pra nao
  // duplicar autostart com a chave Run.
  private static void RemoveLegacyStartupShortcut()
  {
    string lnk = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "coomer.lnk");
    try { if (File.Exists(lnk)) File.Delete(lnk); } catch { }
  }

  private static void PrintUsage()
  {
    Console.WriteLine($"""
      coomer {Version} — zoomer + annotation overlay

      Uso:
        coomer              roda residente (Ctrl+Alt+Z abre, Ctrl+Alt+Q sai)
        coomer --install    inicia junto com o Windows
        coomer --uninstall  remove o autostart
        coomer --version    mostra a versao
        coomer --help       mostra isto
      """);
  }

  // WinExe nao tem console; anexa no terminal pai pra imprimir, depois solta.
  private static void WithConsole(Action body)
  {
    bool attached = AttachConsole(AttachParentProcess);
    try { body(); }
    finally { if (attached) { Console.Out.Flush(); FreeConsole(); } }
  }

  private const int AttachParentProcess = -1;
  [DllImport("kernel32.dll", SetLastError = true)] private static extern bool AttachConsole(int dwProcessId);
  [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FreeConsole();
}
