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

namespace Coomer.App;

/// <summary>
/// Host da aplicacao: cria a janela borderless/topmost cobrindo a tela, abre o
/// contexto GL e roda o loop. E o equivalente ao <c>main()</c> de boomer.nim.
/// </summary>
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
    // Capturar ANTES de abrir a janela (senao o overlay entra na foto) e SO o monitor
    // onde esta o cursor — assim janela e captura tem o mesmo tamanho/origem.
    _screenshot = Screenshot.CaptureMonitorUnderCursor();

    var options = WindowOptions.Default with
    {
      // +1px de altura de proposito: se a janela cobre o monitor EXATO, o Windows
      // engata "fullscreen optimization" e faz uma troca de modo (a tela pisca ao
      // abrir/fechar). 1px a mais (que cai fora da tela) mantem ela como janela
      // composta normal -> sem piscada.
      Size = new Vector2D<int>(_screenshot.Width, _screenshot.Height + 1),
      Position = new Vector2D<int>(_screenshot.OriginX, _screenshot.OriginY),
      Title = "coomer",
      WindowBorder = WindowBorder.Hidden,
      TopMost = true,
      VSync = true,
      IsVisible = false, // comeca invisivel; mostramos so depois do 1o frame (sem piscada)
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
    _renderer = new Renderer(_gl, _screenshot);
    _handler = new InputHandler(_input, _camera, _flashlight, _config, _screenshot,
                                _configPath, _frameRate, _picker, _draw);

    if (_window.Native?.Win32 is { } win32)
      _hwnd = win32.Hwnd;
    OverlayWindowNative.HideFromTaskbar(_hwnd);
    ApplyViewport();
  }

  private void OnUpdate(double delta)
  {
    // dt fixo amarrado ao refresh (mesmo feeling do boomer original).
    float dt = 1f / _frameRate;
    // Usa o tamanho REAL da tela (nao o framebuffer, que tem +1px) pra projecao casar
    // pixel a pixel com o desktop e nao deslocar a imagem.
    var windowSize = new Vector2(_screenshot.Width, _screenshot.Height);

    _handler.Tick(); // sincroniza estado do cursor (hide/show com flashlight)
    _camera.Update(_config, dt, _handler.Dragging, windowSize);
    _flashlight.Update(_config, dt, _handler.CursorPosition);

    if (_handler.Quitting)
      _window.Close();
  }

  private void OnRender(double delta)
  {
    // Mostra a janela so a partir do 2o frame: o 1o ja foi desenhado e apresentado,
    // entao ela aparece direto com a captura (sem o flash do fundo vazio).
    if (_frames == 1 && !_shown)
    {
      _window.IsVisible = true;
      OverlayWindowNative.SetForegroundWindow(_hwnd);
      _shown = true;
    }

    var windowSize = new Vector2(_screenshot.Width, _screenshot.Height);
    _renderer.Draw(_camera, _flashlight, _config, _handler.Mirror, windowSize,
                   _handler.CursorPosition, _draw);
    _frames++;
  }

  private void OnResize(Vector2D<int> size)
  {
    ApplyViewport();
  }

  // Renderiza so na area visivel do monitor. O +1px extra do framebuffer cai embaixo
  // (fora da tela), entao deslocamos o viewport em Y pra imagem cobrir a tela exata.
  private void ApplyViewport()
  {
    int yOff = _window.FramebufferSize.Y - _screenshot.Height;
    _gl.Viewport(0, yOff, (uint)_screenshot.Width, (uint)_screenshot.Height);
  }

  // Libera o input e os recursos de GL ao fechar o overlay. Sem isso, ao reabrir,
  // o GLFW reusa o handle da janela e o Silk crasha com "More than one input context".
  private void OnClosing()
  {
    _handler?.RestoreCursor();
    _renderer?.Dispose();
    _input?.Dispose();
  }
}