# coomer

Zoomer application for Windows.

A C# port of [boomer](https://github.com/tsoding/boomer) by Tsoding, which is
written in Nim for Linux/X11. `coomer` takes a screenshot of the monitor under
the cursor and lets you zoom and pan around it, with an optional flashlight
effect to highlight things.

## Dependencies

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A GPU supporting OpenGL 3.3+

## Quick Start

```console
> dotnet run --project coomer
```

Build a self-contained release (runs without .NET installed):

```console
> dotnet publish coomer -c Release -r win-x64 -p:PublishAot=false --self-contained
```

The output (a folder with `coomer.exe` and its dependencies) lives under
`coomer/bin/Release/net10.0/win-x64/publish/`. Bind `coomer.exe` to a keyboard
shortcut (a Start menu shortcut key, PowerToys, AutoHotkey, etc.) to launch it on
demand, the same way boomer is bound to a WM hotkey.

> For a small single-file binary, build with NativeAOT from a *Developer PowerShell
> for VS* (`dotnet publish coomer -c Release -r win-x64`). The linker needs that
> environment to be on PATH.

## Controls

| Control                                   | Description                              |
|-------------------------------------------|------------------------------------------|
| `0`                                       | Reset position, scale and mirror         |
| `q` or `Esc`                              | Quit                                     |
| `r`                                       | Reload configuration                     |
| `m`                                       | Mirror the image                         |
| `f`                                       | Toggle the flashlight effect             |
| Drag with left mouse button               | Move the image around                    |
| Scroll wheel or `=` / `-`                 | Zoom in/out                              |
| `Ctrl` + Scroll wheel                     | Change the flashlight radius             |

## Configuration

The config file lives at `%APPDATA%\coomer\config` with the format:

```
<param> = <value>
# comment
```

| Name           | Description                                          |
|----------------|------------------------------------------------------|
| min_scale      | Smallest zoom (1.0 = never smaller than full screen) |
| scroll_speed   | How quickly scrolling zooms in/out                   |
| drag_friction  | How quickly panning slows down after a drag          |
| scale_friction | How quickly zooming slows down after scrolling       |

## Differences from boomer

- Captures only the monitor under the cursor instead of the whole X screen.
- Panning is clamped to the image bounds.
- The flashlight radius is a fixed screen size (it does not grow with zoom).
