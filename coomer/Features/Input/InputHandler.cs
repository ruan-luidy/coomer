using System.Numerics;
using Silk.NET.Input;
using Coomer.Features.Navigation;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Lighting;

namespace Coomer.Features.Input;

/// <summary>
/// Porte do bloco de eventos de boomer.nim (KeyPress/ButtonPress/MotionNotify).
/// Liga o teclado/mouse do Silk.NET ao estado da camera, lanterna e mirror.
/// </summary>
public sealed class InputHandler
{
  private readonly Camera _camera;
  private readonly Flashlight _flashlight;
  private readonly Config _config;
  private readonly Screenshot _screenshot;
  private readonly string _configPath;
  private readonly float _frameRate;

  private Mouse _mouse;
  private bool _ctrl;

  public bool Quitting { get; private set; }
  public bool Mirror { get; private set; }
  public Vector2 CursorPosition => _mouse.Current;
  public bool Dragging => _mouse.Drag;

  public InputHandler(IInputContext input, Camera camera, Flashlight flashlight,
                      Config config, Screenshot screenshot, string configPath, float frameRate)
  {
    _camera = camera;
    _flashlight = flashlight;
    _config = config;
    _screenshot = screenshot;
    _configPath = configPath;
    _frameRate = frameRate;

    foreach (var keyboard in input.Keyboards)
    {
      keyboard.KeyDown += OnKeyDown;
      keyboard.KeyUp += OnKeyUp;
    }
    foreach (var mouse in input.Mice)
    {
      mouse.MouseMove += OnMouseMove;
      mouse.MouseDown += OnMouseDown;
      mouse.MouseUp += OnMouseUp;
      mouse.Scroll += OnScroll;
    }
  }

  private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
  {
    switch (key)
    {
      case Key.ControlLeft:
      case Key.ControlRight:
        _ctrl = true;
        break;

      case Key.Number0:
        _camera.Reset();
        Mirror = false;
        break;

      // Q/Esc fecham o overlay no KeyUp (ver OnKeyUp), nao aqui: se fechasse no KeyDown e
      // o usuario segurasse a tecla, o auto-repeat do Windows mandaria os KeyDown seguintes
      // pra janela de baixo (ex.: um video em fullscreen), que sairia do fullscreen. Mantendo
      // o overlay vivo ate soltar, o coomer consome todos os eventos e nada vaza.

      case Key.R:
        if (File.Exists(_configPath))
          _config.Reload(_configPath);
        break;

      case Key.M:
        // Ancora o espelhamento no cursor (mesma conta do boomer.nim).
        _camera.Position.X += _screenshot.Width / _camera.Scale
                            - 2f * (_mouse.Current.X / _camera.Scale + _camera.Position.X);
        Mirror = !Mirror;
        break;

      case Key.F:
        _flashlight.IsEnabled = !_flashlight.IsEnabled;
        break;

      case Key.Equal:
        ScrollUp();
        break;

      case Key.Minus:
        ScrollDown();
        break;
    }
  }

  private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
  {
    if (key is Key.ControlLeft or Key.ControlRight)
      _ctrl = false;
    else if (key is Key.Q or Key.Escape)
      Quitting = true;
  }

  private void OnMouseMove(IMouse mouse, Vector2 position)
  {
    _mouse.Current = position;

    if (_mouse.Drag)
    {
      var delta = _camera.World(_mouse.Previous) - _camera.World(_mouse.Current);
      _camera.Position += delta;
      // delta e a distancia percorrida em 1 frame; * fps vira unidades/segundo.
      _camera.Velocity = delta * _frameRate;
    }

    _mouse.Previous = _mouse.Current;
  }

  private void OnMouseDown(IMouse mouse, MouseButton button)
  {
    if (button == MouseButton.Left)
    {
      _mouse.Previous = _mouse.Current;
      _mouse.Drag = true;
      _camera.Velocity = Vector2.Zero;
    }
  }

  private void OnMouseUp(IMouse mouse, MouseButton button)
  {
    if (button == MouseButton.Left)
      _mouse.Drag = false;
  }

  private void OnScroll(IMouse mouse, ScrollWheel wheel)
  {
    if (wheel.Y > 0) ScrollUp();
    else if (wheel.Y < 0) ScrollDown();
  }

  private void ScrollUp()
  {
    if (_ctrl && _flashlight.IsEnabled)
    {
      _flashlight.DeltaRadius += Flashlight.InitialDeltaRadius;
    }
    else
    {
      _camera.DeltaScale += _config.ScrollSpeed;
      _camera.ScalePivot = _mouse.Current;
    }
  }

  private void ScrollDown()
  {
    if (_ctrl && _flashlight.IsEnabled)
    {
      _flashlight.DeltaRadius -= Flashlight.InitialDeltaRadius;
    }
    else
    {
      _camera.DeltaScale -= _config.ScrollSpeed;
      _camera.ScalePivot = _mouse.Current;
    }
  }
}
