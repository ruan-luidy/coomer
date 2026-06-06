using Coomer.App;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Input.Glfw;

// Registra o backend GLFW na mao. Em publish single-file (e AOT) a descoberta
// automatica de plataforma do Silk.NET nao funciona e Window.Create falha com
// "Couldn't find a suitable window platform".
GlfwWindowing.RegisterPlatform();
GlfwInput.RegisterPlatform();

// DPI awareness ANTES de tudo: senao a captura e o zoom saem borrados/desalinhados
// em telas com escala (hi-DPI). Esse e o erro classico ao portar pra Windows.
Screenshot.EnableDpiAwareness();

string configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "coomer");
string configPath = Path.Combine(configDir, "config");

Config config = File.Exists(configPath)
    ? Config.Load(configPath)
    : Config.Default();

new BoomerApp(config, configPath).Run();
