using Coomer.App;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Hotkey;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

// CLI: --install / --uninstall / --version / --help. Sai sem abrir overlay.
if (Installer.TryHandle(args, out int cliExit))
  return cliExit;

// Instancia unica — senao RegisterHotKey conflita.
using var singleton = new Mutex(true, "coomer-singleton", out bool isNew);
if (!isNew)
  return 0;

// Backend GLFW na mao (necessario em publish, fora do dotnet run).
GlfwWindowing.RegisterPlatform();
GlfwInput.RegisterPlatform();

Screenshot.EnableDpiAwareness();

string configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "coomer");
string luaPath = Path.Combine(configDir, "config.lua");
string legacyPath = Path.Combine(configDir, "config");

new HotkeyHost().Run(onShow: () =>
{
  try
  {
    // re-checa a cada abertura pra que criar o config.lua sem reiniciar
    // o processo residente tambem pegue ele de imediato.
    string configPath = File.Exists(luaPath) ? luaPath
                      : (File.Exists(legacyPath) ? legacyPath : luaPath);

    Config config;
    try { config = File.Exists(configPath) ? Config.Load(configPath) : Config.Default(); }
    catch { config = Config.Default(); }

    new CoomerApp(config, configPath).Run();
  }
  catch
  {
    // Falha no overlay nao pode derrubar o processo residente.
  }
});

return 0;
