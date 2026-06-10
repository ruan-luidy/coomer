# coomer

Zoomer + annotation overlay for Windows.

A C# port of [boomer](https://github.com/tsoding/boomer) by Tsoding (Nim,
Linux/X11). `coomer` captures the monitor under the cursor and pops a zoom +
annotation overlay on a global hotkey, then ducks back to the background. The
process stays resident so the overlay opens instantly.

## Dependencies

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A GPU supporting OpenGL 3.3+

## Quick start

```console
> dotnet run --project coomer
```

Build a self-contained release (runs without .NET installed):

```console
> dotnet publish coomer -c Release -r win-x64 -p:PublishAot=false --self-contained
```

The output lives under `coomer/bin/Release/net10.0/win-x64/publish/`. Run
`coomer.exe` once and it stays in the background. Drop a shortcut into
`shell:startup` to have it ready every login.

> For a small single-file binary, build with NativeAOT from a *Developer
> PowerShell for VS* (`dotnet publish coomer -c Release -r win-x64`). The
> linker needs that environment on PATH.

## Controls

Global (work anywhere while coomer is running):

| Hotkey         | Description                          |
|----------------|--------------------------------------|
| `Ctrl+Alt+Z`   | Open the overlay                     |
| `Ctrl+Alt+Q`   | Quit coomer                          |

Inside the overlay:

| Control                            | Description                                  |
|------------------------------------|----------------------------------------------|
| `0`                                | Reset position, scale and mirror             |
| `q` or `Esc`                       | Close the overlay (coomer keeps running)     |
| `r`                                | Reload configuration                         |
| `m`                                | Mirror the image                             |
| `f`                                | Toggle flashlight                            |
| `c` or `p`                         | Color picker (click a pixel → hex on clipboard) |
| `b`                                | Region copy: drag a rect, release to copy as bitmap |
| `Ctrl+S`                           | Save the annotated screen as BMP in `Pictures/` |
| Drag left mouse                    | Pan                                          |
| Scroll or `=` / `-`                | Zoom                                         |
| `Ctrl` + scroll                    | Flashlight radius or pen thickness           |
| `h` `j` `k` `l` / arrows           | Pan by keyboard                              |

### Drawing (`d` toggles)

In drawing mode left-drag draws instead of panning. Strokes are pinned to the
image and stay above the flashlight shadow.

| Control                  | Description                                                    |
|--------------------------|----------------------------------------------------------------|
| `d`                      | Toggle drawing                                                 |
| `s`                      | Cycle shape: free → line → arrow → rect → circle               |
| Hold `Shift` while drag  | Snap line to 45°, force square / perfect circle                |
| `t`                      | Toggle stamp mode (click drops a numbered badge)               |
| `v`                      | Hide / show strokes + stamps (brush ring stays)                |
| `z`                      | Undo last stroke / stamp                                       |
| `x`                      | Clear everything                                               |
| `[` / `]`                | Pen thickness − / +                                            |
| `,`                      | Cycle pen color (also Ctrl + scroll for thickness)             |

Picked colors stack as swatches in the bottom-right (last 8, LRU).

#### Shape smarts

- **Arrow** is freehand: drag anywhere — straight, curvy, swoosh — and an
  arrowhead lands at the end pointing along your final stroke direction.
- **Circle** drag is an axis-aligned ellipse inscribed in the bbox. Hold
  `Shift` to lock it to a perfect circle.
- **Free → auto-shape**: finish a rough closed scribble and it gets recognized
  as a circle or ellipse (any orientation) and replaced by the clean version.
  Fit is by PCA of the points; you can disable with `DrawTool.AutoCircle = false`.

## Configuration

`%APPDATA%\coomer\config`, plain `key = value`:

| Name           | Description                                          |
|----------------|------------------------------------------------------|
| min_scale      | Smallest zoom (1.0 = never smaller than full screen) |
| scroll_speed   | How quickly scrolling zooms in/out                   |
| drag_friction  | How quickly panning slows down after a drag          |
| scale_friction | How quickly zooming slows down after scrolling       |
| pan_inertia    | `false` = image stops with the mouse (no glide)      |
| bubble_rigid   | `true` = flashlight snaps to cursor (no spring lerp/deform) |

Press `r` inside the overlay to reload without restarting.

## Differences from boomer

- Captures only the monitor under the cursor instead of the whole X screen.
- Panning is clamped to the image bounds.
- The flashlight radius is a fixed screen size (it does not grow with zoom).
- Runs resident with a global hotkey instead of being launched per use.
- Adds an annotation layer (pen, line, arrow, rect, circle/ellipse, numbered
  stamps), a region-to-clipboard tool and a save-to-disk hotkey.
