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
using Coomer.Features.Effects;

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
  private RippleEffect _ripple = null!;
  private float _frameRate = 60f;
  private nint _hwnd;
  private int _frames;
  private bool _shown;

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
    _ripple = new RippleEffect();

    _renderer = new Renderer(_gl, _screenshot);
    _handler = new InputHandler(_input, _camera, _flashlight, _config, _screenshot,
                                _configPath, _frameRate, _picker, _draw, _exporter,
                                _stickers, _stickerState, _ripple);

    if (_window.Native?.Win32 is { } win32)
      _hwnd = win32.Hwnd;
    OverlayWindowNative.HideFromTaskbar(_hwnd);
    ApplyViewport();
  }

  private void OnUpdate(double delta)
  {
    float dt = 1f / _frameRate;
    var windowSize = new Vector2(_screenshot.Width, _screenshot.Height);

    _handler.Tick();
    _camera.Update(_config, dt, _handler.Dragging, windowSize);
    _flashlight.Update(_config, dt, _handler.CursorPosition);
    _exporter.TickStatus(dt);
    _ripple.Tick(dt);

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

    var windowSize = new Vector2(_screenshot.Width, _screenshot.Height);
    _renderer.Draw(_camera, _flashlight, _config, _handler.Mirror, windowSize,
                   _handler.CursorPosition, _draw, _picker.History, _exporter,
                   _stickers, _stickerState, _picker, _ripple);

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
    int yOff = _window.FramebufferSize.Y - _screenshot.Height;
    _gl.Viewport(0, yOff, (uint)_screenshot.Width, (uint)_screenshot.Height);
  }

  private void OnClosing()
  {
    _handler?.RestoreCursor();
    _renderer?.Dispose();
    _stickers?.Dispose();
    _input?.Dispose();
  }
}
