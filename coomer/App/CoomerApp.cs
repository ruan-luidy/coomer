using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Coomer.Features.Navigation;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Lighting;
using Coomer.Features.Input;
using Coomer.Features.Rendering;
using Coomer.Features.Drawing;
using Coomer.Features.Stickers;

namespace Coomer.App;

public sealed class CoomerApp
{
  private readonly Config _config;
  private readonly string _configPath;

  private IWindow _window = null!;
  private GL _gl = null!;
  private IInputContext _input = null!;
  private Screenshot _screenshot = null!;
  private Renderer _renderer = null!;
  private InputHandler _handler = null!;
  private Camera _camera = null!;
  private Flashlight _flashlight = null!;
  private ColorPicker _picker = null!;
  private DrawTool _draw = null!;
  private RegionExporter _exporter = null!;
  private StickerCache _stickers = null!;
  private StickerState _stickerState = null!;
  private float _frameRate = 60f;
  private nint _hwnd;
  private int _frames;
  private bool _shown;
  private bool _stabilized;

  public CoomerApp(Config config, string configPath)
  {
    _config = config;
    _configPath = configPath;
  }

  public void Run()
  {
    _screenshot = Screenshot.CaptureMonitorUnderCursor();

    // +1px na altura: cobrir o monitor exato dispara fullscreen-optimization
    // do Windows e a tela pisca; sobrar 1px fora mantem janela composta.
    var options = WindowOptions.Default with
    {
      Size = new Vector2D<int>(_screenshot.Width, _screenshot.Height + 1),
      Position = new Vector2D<int>(_screenshot.OriginX, _screenshot.OriginY),
      Title = "coomer",
      WindowBorder = WindowBorder.Hidden,
      TopMost = true,
      VSync = true,
      IsVisible = false,
      // canal alpha no framebuffer: o marca-texto acumula cobertura nele
      PreferredBitDepth = new Vector4D<int>(8, 8, 8, 8),
    };

    _window = Window.Create(options);
    _window.Load += OnLoad;
    _window.Update += OnUpdate;
    _window.Render += OnRender;
    _window.FramebufferResize += OnResize;
    _window.Closing += OnClosing;

    _window.Run();
    _window.Dispose();
  }

  private void OnLoad()
  {
    _gl = _window.CreateOpenGL();
    _input = _window.CreateInput();

    if (_window.Monitor?.VideoMode.RefreshRate is int rate and > 0)
      _frameRate = rate;

    _camera = new Camera(new Vector2(_screenshot.Width, _screenshot.Height));
    _flashlight = new Flashlight();
    _picker = new ColorPicker();
    _draw = new DrawTool();
    _exporter = new RegionExporter();

    var stickerDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "coomer", "stickers");
    _stickers = new StickerCache(_gl, stickerDir);
    _stickers.Reload();
    _stickerState = new StickerState();
    _stickerState.RefreshFrom(_stickers);

    _renderer = new Renderer(_gl, _screenshot);
    _handler = new InputHandler(_input, _camera, _flashlight, _config, _screenshot,
                                _configPath, _frameRate, _picker, _draw, _exporter,
                                _stickers, _stickerState);

    if (_window.Native?.Win32 is { } win32)
      _hwnd = win32.Hwnd;
    Clipboard.Owner = _hwnd;
    OverlayWindowNative.HideFromTaskbar(_hwnd);
    ApplyViewport();
  }

  private void OnUpdate(double delta)
  {
    // dt real (clampado): com o 1/refresh fixo a fisica corria mais rapido
    // quando o refresh do monitor nao era detectado e lerda quando frame caia.
    float dt = delta > 0 ? (float)Math.Clamp(delta, 1.0 / 1000.0, 1.0 / 30.0) : 1f / _frameRate;
    var windowSize = new Vector2(_screenshot.Width, _screenshot.Height);

    _handler.Tick();
    _camera.Update(_config, dt, _handler.Dragging, windowSize);
    // congela a bolha no modo print pra dar pra capturar ela junto
    if (!_exporter.Active)
      _flashlight.Update(_config, dt, _handler.CursorPosition);
    _exporter.TickStatus(dt);

    if (_handler.Quitting)
      _window.Close();
  }

  private void OnRender(double delta)
  {
    if (_frames == 1 && !_shown)
    {
      _window.IsVisible = true;
      OverlayWindowNative.SetForegroundWindow(_hwnd);
      _shown = true;
    }
    else if (_frames == 3 && !_stabilized)
    {
      // No frame 3 a janela ja foi mostrada e o desktop ja estabilizou
      // (jogo em fullscreen exclusivo ja entregou o display de volta,
      // mudanca de DPI ja propagou). Recaptura e ve se as dimensoes do
      // monitor mudaram desde o snapshot inicial.
      _stabilized = true;
      TryStabilize();
    }

    var windowSize = new Vector2(_screenshot.Width, _screenshot.Height);
    _renderer.Draw(_camera, _flashlight, _config, _handler.Mirror, windowSize,
                   _handler.CursorPosition, _draw, _picker.History, _exporter,
                   _stickers, _stickerState, _picker, _handler.ShowHelp);

    // Depois do Draw e antes do swap: framebuffer ja tem o composite final.
    int yOff = _window.FramebufferSize.Y - _screenshot.Height;
    _exporter.FlushAfterRender(_gl, yOff, _screenshot.Width, _screenshot.Height);

    _frames++;
  }

  private void OnResize(Vector2D<int> size)
  {
    ApplyViewport();
  }

  private void ApplyViewport()
  {
    // Cobre o framebuffer inteiro sempre. Quando tudo da certo (DPI propagou,
    // sem race com fullscreen exclusivo) framebuffer == screenshot+1 e a
    // imagem renderiza 1:1; quando da errado (jogo saiu de exclusivo deixando
    // a janela com dim antiga, ou GLFW nao honrou PerMonitorV2 e voltou
    // framebuffer escalado) a imagem estica pra preencher tudo em vez de
    // ficar num pedacinho pequeno no canto.
    var fb = _window.FramebufferSize;
    _gl.Viewport(0, 0, (uint)fb.X, (uint)fb.Y);
  }

  private void TryStabilize()
  {
    Screenshot fresh;
    try { fresh = Screenshot.CaptureMonitorUnderCursor(); }
    catch { return; }

    if (fresh.Width == _screenshot.Width
     && fresh.Height == _screenshot.Height
     && fresh.OriginX == _screenshot.OriginX
     && fresh.OriginY == _screenshot.OriginY)
      return;

    // Monitor mudou de dimensao desde a captura inicial — recria texture/quad
    // com a nova resolucao e redimensiona a janela pro novo retangulo do
    // monitor. Camera e estado de input ficam preservados.
    var oldRenderer = _renderer;
    _screenshot = fresh;
    _renderer = new Renderer(_gl, _screenshot);
    oldRenderer.Dispose();
    _camera.ResizeImage(new Vector2(_screenshot.Width, _screenshot.Height));

    _window.Size = new Vector2D<int>(_screenshot.Width, _screenshot.Height + 1);
    _window.Position = new Vector2D<int>(_screenshot.OriginX, _screenshot.OriginY);
    ApplyViewport();
  }

  private void OnClosing()
  {
    _handler?.RestoreCursor();
    _renderer?.Dispose();
    _stickers?.Dispose();
    _input?.Dispose();
  }
}
