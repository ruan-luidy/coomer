using System.Numerics;
using Silk.NET.Input;
using Coomer.Features.Navigation;
using Coomer.Features.Capture;
using Coomer.Features.Configuration;
using Coomer.Features.Lighting;
using Coomer.Features.Drawing;
using Coomer.Features.Stickers;
using Coomer.App;

namespace Coomer.Features.Input;

public sealed class InputHandler
{
  private readonly Camera _camera;
  private readonly Flashlight _flashlight;
  private readonly Config _config;
  private readonly Screenshot _screenshot;
  private readonly string _configPath;
  private readonly float _frameRate;
  private readonly ColorPicker _picker;
  private readonly DrawTool _draw;
  private readonly RegionExporter _exporter;
  private readonly StickerCache _stickers;
  private readonly StickerState _stickerState;

  private Mouse _mouse;
  private bool _ctrl;
  private bool _shift;
  private bool _space;
  private bool _spacePan; // commit no mousedown: drag virou pan ate o mouseup
  private bool _flashlightCursorHidden;

  public bool Quitting { get; private set; }
  public bool Mirror { get; private set; }
  public Vector2 CursorPosition => _mouse.Current;
  public bool Dragging => _mouse.Drag;
  public ColorPicker Picker => _picker;
  public DrawTool Draw => _draw;
  public RegionExporter Exporter => _exporter;
  public StickerState StickerState => _stickerState;

  public InputHandler(IInputContext input, Camera camera, Flashlight flashlight,
                      Config config, Screenshot screenshot, string configPath,
                      float frameRate, ColorPicker picker, DrawTool draw,
                      RegionExporter exporter, StickerCache stickers, StickerState stickerState)
  {
    _camera = camera;
    _flashlight = flashlight;
    _config = config;
    _screenshot = screenshot;
    _configPath = configPath;
    _frameRate = frameRate;
    _picker = picker;
    _draw = draw;
    _exporter = exporter;
    _stickers = stickers;
    _stickerState = stickerState;

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

  public void Tick()
  {
    bool wantHide = _flashlight.IsEnabled && _config.HideCursorOnFlashlight;
    if (wantHide != _flashlightCursorHidden)
    {
      OverlayWindowNative.SetCursorVisible(!wantHide);
      _flashlightCursorHidden = wantHide;
    }
    _draw.ShiftHeld = _shift;
  }

  public void RestoreCursor()
  {
    if (_flashlightCursorHidden)
    {
      OverlayWindowNative.SetCursorVisible(true);
      _flashlightCursorHidden = false;
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

      case Key.ShiftLeft:
      case Key.ShiftRight:
        _shift = true;
        break;

      case Key.Space:
        _space = true;
        break;

      case Key.Number0:
        _camera.Reset(_config.LerpCameraRecenter);
        Mirror = false;
        break;

      case Key.R:
        if (File.Exists(_configPath))
          _config.Reload(_configPath);
        _stickers.Reload();
        _stickerState.RefreshFrom(_stickers);
        break;

      case Key.M:
        _camera.Position.X += _screenshot.Width / _camera.Scale
                            - 2f * (_mouse.Current.X / _camera.Scale + _camera.Position.X);
        Mirror = !Mirror;
        break;

      case Key.F:
        _flashlight.IsEnabled = !_flashlight.IsEnabled;
        if (_flashlight.IsEnabled) _draw.IsEnabled = false;
        break;

      case Key.C:
      case Key.P:
        _picker.IsEnabled = !_picker.IsEnabled;
        if (_picker.IsEnabled && _flashlight.IsEnabled) _flashlight.IsEnabled = false;
        if (_picker.IsEnabled) _draw.IsEnabled = false;
        break;

      case Key.D:
        _draw.IsEnabled = !_draw.IsEnabled;
        if (_draw.IsEnabled)
        {
          _flashlight.IsEnabled = false;
          _picker.IsEnabled = false;
        }
        break;
      case Key.S:
        if (_ctrl) _exporter.RequestSaveFull();
        else if (_draw.IsEnabled) _draw.CycleShape();
        break;
      case Key.Z:
        if (_draw.IsEnabled) _draw.Undo();
        break;
      case Key.X:
        if (_draw.IsEnabled) _draw.Clear();
        break;
      case Key.LeftBracket:
        if (_draw.IsEnabled && _draw.StickerMode) _draw.StickerSizeDelta(-8f);
        else if (_draw.IsEnabled) _draw.ThicknessDelta(-1f);
        break;
      case Key.RightBracket:
        if (_draw.IsEnabled && _draw.StickerMode) _draw.StickerSizeDelta(+8f);
        else if (_draw.IsEnabled) _draw.ThicknessDelta(+1f);
        break;
      case Key.Comma:
        if (_draw.IsEnabled) _draw.CycleColor();
        break;
      case Key.V:
        if (_draw.IsEnabled) _draw.Hide = !_draw.Hide;
        break;
      case Key.T:
        if (_draw.IsEnabled) { _draw.StampMode = !_draw.StampMode; if (_draw.StampMode) _draw.StickerMode = false; }
        break;
      case Key.Y:
        _draw.StickerMode = !_draw.StickerMode;
        if (_draw.StickerMode)
        {
          _draw.IsEnabled = true;
          _draw.StampMode = false;
          _flashlight.IsEnabled = false;
          _picker.IsEnabled = false;
          if (_stickerState.Current == null) _stickerState.RefreshFrom(_stickers);
        }
        break;
      case Key.Semicolon:
      case Key.Tab:
        if (_draw.IsEnabled && _draw.StickerMode)
          _stickerState.CycleSticker(_stickers, _shift ? -1 : +1);
        break;
      case Key.Apostrophe:
      case Key.GraveAccent:
        if (_draw.IsEnabled && _draw.StickerMode)
          _stickerState.CycleCategory(_stickers, _shift ? -1 : +1);
        break;

      case Key.B:
        _exporter.ToggleCopy();
        if (_exporter.Active)
        {
          _draw.IsEnabled = false;
          _picker.IsEnabled = false;
        }
        break;

      case Key.H:
      case Key.Left:
        _camera.Pan(new Vector2(-_config.CameraPanAmount, 0));
        break;
      case Key.L:
      case Key.Right:
        _camera.Pan(new Vector2(_config.CameraPanAmount, 0));
        break;
      case Key.K:
      case Key.Up:
        _camera.Pan(new Vector2(0, -_config.CameraPanAmount));
        break;
      case Key.J:
      case Key.Down:
        _camera.Pan(new Vector2(0, _config.CameraPanAmount));
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
    if (key is Key.ControlLeft or Key.ControlRight) _ctrl = false;
    else if (key is Key.ShiftLeft or Key.ShiftRight) _shift = false;
    else if (key is Key.Space) _space = false;
    else if (key is Key.Q or Key.Escape)
    {
      if (_exporter.Active) { _exporter.Cancel(); return; }
      Quitting = true;
    }
  }

  private void OnMouseMove(IMouse mouse, Vector2 position)
  {
    _mouse.Current = position;

    if (_exporter.Dragging)
    {
      _exporter.Move(_mouse.Current);
      _mouse.Previous = _mouse.Current;
      return;
    }

    if (_draw.IsEnabled && !_draw.StampMode && !_draw.StickerMode && _mouse.Drag && !_spacePan)
    {
      _draw.Move(_mouse.Current, new Vector2(_screenshot.Width, _screenshot.Height),
                 _screenshot, _camera, Mirror);
      _mouse.Previous = _mouse.Current;
      return;
    }

    if (_mouse.Drag)
    {
      var delta = _camera.World(_mouse.Previous) - _camera.World(_mouse.Current);
      _camera.Position += delta;
      _camera.Velocity = delta * _frameRate;
      _camera.LerpingToTarget = false;
    }

    _mouse.Previous = _mouse.Current;
  }

  private void OnMouseDown(IMouse mouse, MouseButton button)
  {
    if (button == MouseButton.Left && _picker.IsEnabled)
    {
      string hex = _picker.PickAt(_mouse.Current, _camera, _screenshot,
                                   new Vector2(_screenshot.Width, _screenshot.Height));
      _picker.LastHex = hex;
      _picker.IsEnabled = false;
      TryCopyToClipboard(hex);
      return;
    }

    if (button == MouseButton.Left && _exporter.Active)
    {
      _exporter.BeginDrag(_mouse.Current);
      return;
    }

    if (button == MouseButton.Left && _draw.IsEnabled && _draw.StickerMode && _stickerState.Current != null && !_space)
    {
      _draw.DropSticker(_mouse.Current, new Vector2(_screenshot.Width, _screenshot.Height),
                        _screenshot, _camera, Mirror, _stickerState.Current.Path);
      return;
    }

    if (button == MouseButton.Left && _draw.IsEnabled && _draw.StampMode && !_space)
    {
      _draw.DropStamp(_mouse.Current, new Vector2(_screenshot.Width, _screenshot.Height),
                      _screenshot, _camera, Mirror);
      return;
    }

    if (button == MouseButton.Left && _draw.IsEnabled && !_space)
    {
      _mouse.Previous = _mouse.Current;
      _mouse.Drag = true;
      _draw.Begin(_mouse.Current, new Vector2(_screenshot.Width, _screenshot.Height),
                  _screenshot, _camera, Mirror);
      return;
    }

    // space segurado em draw mode = pan temporario (classico)
    if (button == MouseButton.Left && _draw.IsEnabled && _space)
    {
      _mouse.Previous = _mouse.Current;
      _mouse.Drag = true;
      _spacePan = true;
      _camera.Velocity = Vector2.Zero;
      _camera.LerpingToTarget = false;
      return;
    }

    if (button == MouseButton.Left)
    {
      _mouse.Previous = _mouse.Current;
      _mouse.Drag = true;
      _camera.Velocity = Vector2.Zero;
      _camera.LerpingToTarget = false;
    }

    else if (button == MouseButton.Middle)
    {
      _camera.Reset(_config.LerpCameraRecenter);
      Mirror = false;
    }
  }

  private void OnMouseUp(IMouse mouse, MouseButton button)
  {
    if (button == MouseButton.Left)
    {
      if (_exporter.Dragging) _exporter.Finish();
      if (_draw.IsEnabled && !_draw.StampMode && !_draw.StickerMode && !_spacePan) _draw.End();
      _spacePan = false;
      _mouse.Drag = false;
    }
  }

  private void OnScroll(IMouse mouse, ScrollWheel wheel)
  {
    if (wheel.Y > 0) ScrollUp();
    else if (wheel.Y < 0) ScrollDown();
  }

  private void ScrollUp()
  {
    if (_ctrl && _flashlight.IsEnabled) { _flashlight.DeltaRadius += Flashlight.InitialDeltaRadius; }
    else if (_ctrl && _draw.IsEnabled && _draw.StickerMode) { _draw.StickerSizeDelta(+16f); }
    else if (_ctrl && _draw.IsEnabled) { _draw.ThicknessDelta(+1f); }
    else
    {
      _camera.DeltaScale += _config.ScrollSpeed;
      _camera.ScalePivot = _mouse.Current;
    }
  }

  private void ScrollDown()
  {
    if (_ctrl && _flashlight.IsEnabled) { _flashlight.DeltaRadius -= Flashlight.InitialDeltaRadius; }
    else if (_ctrl && _draw.IsEnabled && _draw.StickerMode) { _draw.StickerSizeDelta(-16f); }
    else if (_ctrl && _draw.IsEnabled) { _draw.ThicknessDelta(-1f); }
    else
    {
      _camera.DeltaScale -= _config.ScrollSpeed;
      _camera.ScalePivot = _mouse.Current;
    }
  }

  private static void TryCopyToClipboard(string text)
  {
    try
    {
      var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c clip")
      {
        RedirectStandardInput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
      };
      using var p = System.Diagnostics.Process.Start(psi);
      if (p != null)
      {
        p.StandardInput.Write(text);
        p.StandardInput.Close();
        p.WaitForExit(500);
      }
    }
    catch { }
  }
}
