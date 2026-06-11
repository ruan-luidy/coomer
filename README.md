# coomer

Zoomer + annotation overlay for Windows.

A C# port of [boomer](https://github.com/tsoding/boomer) by Tsoding (Nim,
Linux/X11). `coomer` captures the monitor under the cursor and pops a zoom +
annotation overlay on a global hotkey, then ducks back to the background. The
process stays resident so the overlay opens instantly.

## Install

### Scoop (recommended)

```console
> scoop install https://raw.githubusercontent.com/ruan-luidy/coomer/main/scoop/coomer.json
```

This drops a single self-contained `coomer.exe` (no .NET runtime needed). Run
`coomer` once and it stays resident; open the overlay with `Ctrl+Alt+Z`.

Update later with:

```console
> scoop update coomer
```

### Manual

Grab `coomer-vX.Y.Z-win-x64.zip` from the
[releases](https://github.com/ruan-luidy/coomer/releases), unzip
(`coomer.exe` + `glfw3.dll`), and run `coomer.exe`.

### Start with Windows

```console
> coomer --install     # start on login
> coomer --uninstall   # stop starting on login
> coomer --version
```

`--install` registers the current `coomer.exe` under the user's `Run` key, so
it works for both the Scoop and manual installs.

## Build from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) and a GPU
supporting OpenGL 3.3+.

Run straight from source:

```console
> dotnet run --project coomer
```

Build the single-file release (NativeAOT, self-contained — same artifact Scoop
ships):

```console
> .\build.ps1
```

The output lands in `dist\` (`coomer.exe` + `glfw3.dll`). `build.ps1` puts
`vswhere` on `PATH` so the native linker finds the MSVC toolchain on its own —
no *Developer PowerShell for VS* needed. Pass `-Version 0.2.1` to stamp a
version, or `-Install` to enable autostart right after building.

> Need the old framework-dependent build instead? `dotnet publish coomer -c
> Release -r win-x64 -p:PublishAot=false --self-contained`.

## Releasing

Push a `vX.Y.Z` tag and the [release workflow](.github/workflows/release.yml)
builds the AOT exe, zips it, creates the GitHub release, and bumps
`scoop/coomer.json` with the new url + hash:

```console
> git tag -a v0.2.1 -m "..."
> git push --follow-tags
```

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
