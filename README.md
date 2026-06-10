# coomer

Zoomer application for Windows.

A C# port of [boomer](https://github.com/tsoding/boomer) by Tsoding, which is
written in Nim for Linux/X11. `coomer` takes a screenshot of the monitor under
the cursor and lets you zoom and pan around it, with an optional flashlight
effect to highlight things.

It runs resident in the background and pops the overlay on a global hotkey
(`Ctrl+Alt+Z`), so it opens instantly without spinning up a new process each time.

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
`coomer/bin/Release/net10.0/win-x64/publish/`. Run `coomer.exe` once and it stays
in the background waiting for the hotkey. Drop a shortcut to it in your Startup
folder (`shell:startup`) to have it ready on every login.

> For a small single-file binary, build with NativeAOT from a *Developer PowerShell
> for VS* (`dotnet publish coomer -c Release -r win-x64`). The linker needs that
> environment to be on PATH.

## Controls

Global hotkeys (work anywhere while coomer is running in the background):

| Hotkey         | Description                          |
|----------------|--------------------------------------|
| `Ctrl+Alt+Z`   | Open the zoom overlay                |
| `Ctrl+Alt+Q`   | Quit coomer (stop the background app)|

Inside the overlay:

| Control                                   | Description                              |
|-------------------------------------------|------------------------------------------|
| `0`                                       | Reset position, scale and mirror         |
| `q` or `Esc`                              | Close the overlay (coomer keeps running) |
| `r`                                       | Reload configuration                     |
| `m`                                       | Mirror the image                         |
| `f`                                       | Toggle the flashlight effect             |
| Drag with left mouse button               | Move the image around                    |
| Scroll wheel or `=` / `-`                 | Zoom in/out                              |
| `Ctrl` + Scroll wheel                     | Change the flashlight radius / pen thickness |

Drawing mode (toggle with `d` — replaces left-drag panning with the pen):

| Control                  | Description                                          |
|--------------------------|------------------------------------------------------|
| `d`                      | Toggle drawing mode (exclusive with flashlight/picker) |
| `s`                      | Cycle shape: freehand → straight line → rectangle    |
| `z`                      | Undo last stroke                                     |
| `x`                      | Clear all strokes                                    |
| `[` / `]`                | Decrease / increase pen thickness                    |
| `,`                      | Cycle stroke color                                   |
| Drag left mouse          | Draw (freehand smoothed, or shape from press to release) |

Strokes are pinned to the image — they pan/zoom/mirror with the screenshot,
and they live above the flashlight shadow so they stay visible.

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
| pan_inertia    | `false` = image stops with the mouse (no glide)      |
| bubble_rigid   | `true` = flashlight snaps to cursor (no spring lerp/deform) |

## Differences from boomer

- Captures only the monitor under the cursor instead of the whole X screen.
- Panning is clamped to the image bounds.
- The flashlight radius is a fixed screen size (it does not grow with zoom).
- Runs resident with a global hotkey instead of being launched per use.
