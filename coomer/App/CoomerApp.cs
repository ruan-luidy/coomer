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
  private BlackHole _blackHole = null!;
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
    _blackHole = new BlackHole();
    _blackHole.ModeChanged += OnBlackHoleModeChanged;

    _renderer = new Renderer(_gl, _screenshot);
    _handler = new InputHandler(_input, _camera, _flashlight, _config, _screenshot,
                                _configPath, _frameRate, _picker, _draw, _exporter,
                                _stickers, _stickerState, _ripple, _blackHole);

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
    // congela a bolha no modo print pra dar pra capturar ela junto
    if (!_exporter.Active)
      _flashlight.Update(_config, dt, _handler.CursorPosition);
    _exporter.TickStatus(dt);
    _ripple.Tick(dt);
    _blackHole.Tick(dt);

    if (_blackHole.Passthrough)
    {
      // pet mode: gravidade puxa o cursor real do windows quando ele chega
      // perto do buraco (opcao A do mouse-physics — fisica de massa de teste).
      ApplyMouseGravity(dt);

      // refresca a textura do screenshot a ~30Hz com BitBlt da tela viva
      // (o overlay ta WDA_EXCLUDEFROMCAPTURE entao nao se grava). Atualiza
      // em frames pares pra reduzir custo CPU/GPU em 4K.
      if ((_frames & 1) == 0)
      {
        try
        {
          _screenshot.Refresh();
          _renderer.UploadScreenshot();
        }
        catch { /* nao deixa o overlay morrer por uma falha de captura */ }
      }
    }

    if (_handler.Quitting)
      _window.Close();
  }

  // Newton-ish: pull ~1/r² em direcao ao centro, ate dist=5 raios. Sem
  // teleporte ao entrar no horizonte — o cursor so e atraido, sem cuspida
  // do outro lado.
  private void ApplyMouseGravity(float dt)
  {
    if (!OverlayWindowNative.TryGetCursorPos(out int cx, out int cy)) return;

    float W = _screenshot.Width;
    float H = _screenshot.Height;
    float monX = _screenshot.OriginX;
    float monY = _screenshot.OriginY;

    float cw_x = cx - monX;
    float cw_y = cy - monY;

    var uv = _blackHole.ComputeHoleCenterUv();
    float hx = uv.X * W;
    float hy = uv.Y * H;

    float rh = 0.04f * H * _blackHole.HoleScale;

    float dx = hx - cw_x;
    float dy = hy - cw_y;
    float r  = MathF.Sqrt(dx * dx + dy * dy);
    if (r < 1e-3f) return;

    // Fora da zona de influencia: nao mexe
    if (r > 5f * rh) return;

    float ux = dx / r;
    float uy = dy / r;
    float strength = (rh * rh) / (r * r);
    float pull = strength * 700f * dt;
    if (pull > r * 0.5f) pull = r * 0.5f;

    int tx = (int)MathF.Round(cw_x + ux * pull + monX);
    int ty = (int)MathF.Round(cw_y + uy * pull + monY);
    OverlayWindowNative.MoveCursorTo(tx, ty);
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
                   _stickers, _stickerState, _picker, _ripple, _blackHole);

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
    // garante que o hook de teclado nao sobreviva ao fechamento do overlay
    OverlayWindowNative.UninstallEscapeHook();
    OverlayWindowNative.SetClickThrough(_hwnd, false);
    OverlayWindowNative.SetExcludeFromCapture(_hwnd, false);
    _renderer?.Dispose();
    _stickers?.Dispose();
    _input?.Dispose();
  }

  private void OnBlackHoleModeChanged(BlackHoleMode mode)
  {
    bool passthrough = mode == BlackHoleMode.Passthrough;
    OverlayWindowNative.SetClickThrough(_hwnd, passthrough);
    // exclui a janela das proprias capturas: a textura e atualizada por
    // BitBlt da tela inteira no OnUpdate, e sem isso o overlay ficaria
    // gravando a si mesmo (feedback loop visual).
    OverlayWindowNative.SetExcludeFromCapture(_hwnd, passthrough);

    if (passthrough)
    {
      // ouve Esc global pra voltar pro interactive — o overlay perde foco
      // assim que o usuario clica em outra coisa, entao o KeyUp local nao
      // pegaria mais. Acao volta na thread do hook (a do overlay), entao
      // mexer em _blackHole.ExitPassthrough() ali e seguro.
      OverlayWindowNative.InstallEscapeHook(() => _blackHole.ExitPassthrough());
      // mostra o cursor (provavelmente escondido pela flashlight do estado anterior)
      _handler?.RestoreCursor();
    }
    else
    {
      OverlayWindowNative.UninstallEscapeHook();
    }
  }
}
