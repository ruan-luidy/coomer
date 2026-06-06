using Coomer.App;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Hotkey;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

// Instancia unica: se ja tem um coomer rodando, sai (senao o RegisterHotKey conflita).
using var singleton = new Mutex(true, "coomer-singleton", out bool isNew);
if (!isNew)
  return;

// Registra o backend GLFW na mao (necessario fora do dotnet run / em publish).
GlfwWindowing.RegisterPlatform();
GlfwInput.RegisterPlatform();

// DPI awareness antes de qualquer captura/janela.
Screenshot.EnableDpiAwareness();

string configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "coomer");
string configPath = Path.Combine(configDir, "config");

// coomer fica residente em segundo plano. Ctrl+Alt+Z abre o overlay (recapturando a
// tela e recarregando a config na hora); Ctrl+Alt+Q encerra. q/ESC fecha so o overlay.
new HotkeyHost().Run(onShow: () =>
{
  var config = File.Exists(configPath) ? Config.Load(configPath) : Config.Default();
  new BoomerApp(config, configPath).Run();
});
