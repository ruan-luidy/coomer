# roteiro

mapa rapido de onde cada coisa mora — pra nao ter que cacar.

## arquitetura

- `Program.cs` — entry point. mutex de instancia unica, registra hotkeys globais, loop de mensagens.
- `App/CoomerApp.cs` — host da janela GL/overlay: cria a Screenshot, Renderer, InputHandler etc. e roda o Silk.NET window loop.
- `App/OverlayWindowNative.cs` — P/Invoke pra esconder da taskbar e trazer pra frente.
- `Features/Capture/` — Screenshot (BitBlt do monitor sob o cursor) e RegionExporter (region copy CF_DIB + save full BMP via glReadPixels).
- `Features/Configuration/Config.cs` — load/save/reload do arquivo `%APPDATA%/coomer/config`.
- `Features/Drawing/` — DrawTool (estado), Stroke/Stamp (data), StrokeRenderer (geometria + AA), OneEuroFilter (smoothing do cursor).
- `Features/Hotkey/` — registra Ctrl+Alt+Z (open overlay) e Ctrl+Alt+Q (quit).
- `Features/Input/InputHandler.cs` — roteia teclado/mouse pras ferramentas certas.
- `Features/Lighting/` — Flashlight (efeito de bolha) e ColorPicker (+ ColorHistory).
- `Features/Navigation/Camera.cs` — pan/zoom com clamp e lerp.
- `Features/Rendering/` — Renderer (quad da screenshot), Shader (compile + uniforms).
- `Shaders/` — `vert.glsl` + `frag.glsl` da screenshot, `stroke.vert.glsl` + `stroke.frag.glsl` dos tracos.

## fluxo do overlay

1. hotkey -> `CoomerApp.Run()`.
2. captura screenshot do monitor sob o cursor.
3. abre janela borderless TopMost com `screenshot.Height + 1` de altura (1px extra evita fullscreen-optimization piscando).
4. loop Silk.NET: `OnUpdate` (camera + flashlight + tick do exporter), `OnRender` (Renderer.Draw -> StrokeRenderer.Draw -> exporter.FlushAfterRender).
5. esc/q fecha so o overlay; o processo residente continua escutando hotkey.

## tecla -> acao

| tecla        | acao                                           |
|--------------|------------------------------------------------|
| ctrl+alt+z   | abre overlay (global)                          |
| ctrl+alt+q   | encerra coomer (global)                        |
| q / esc      | fecha overlay                                  |
| 0            | reset camera                                   |
| r            | reload config                                  |
| m            | mirror                                         |
| f            | flashlight                                     |
| c / p        | color picker (copia hex pro clipboard)         |
| d            | modo desenho                                   |
| s            | cicla shape (free -> line -> arrow -> rect -> circle); ctrl+s = save full |
| t            | toggle stamp mode (click = bolinha numerada)   |
| v            | toggle visibilidade dos tracos                 |
| z / x        | undo / clear                                   |
| [ / ]        | espessura -1 / +1                              |
| ,            | cicla cor                                      |
| b            | region copy mode (drag rect, copia clipboard)  |
| h/j/k/l      | pan                                            |
| shift        | snap em line (45°) / quadrado em rect / circulo |
| scroll / =-  | zoom                                           |
| ctrl+scroll  | espessura ou raio da lanterna                  |

## shape recognition

`DrawTool.End()` no Free roda `TryDetectEllipse`:
- PCA dos pontos -> autovalores -> semi-eixos `sqrt(2 * lambda_i)`.
- aceita se RMS algebrico `(x'/a)^2 + (y'/b)^2 - 1` < 0.35 e varredura angular >= 1.5pi.
- substitui o stroke por Circle com 3 pontos (centro + eixos).
- desliga com `DrawTool.AutoCircle = false`.

## TODO

- loupe flutuante (Y): viewport pequeno seguindo cursor com sample da textura da screenshot.
- OCR (O): drag rect -> Windows.Media.Ocr via WinRT bridge ou PowerShell -> clipboard.
- save em PNG: encoder zlib + CRC32 + chunks IHDR/IDAT/IEND.
