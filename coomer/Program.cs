using Coomer.App;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;

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
