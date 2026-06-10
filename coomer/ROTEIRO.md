# Leva 1 — UX e identidade visual

5 features: Iosevka + text rendering · HUD · sticker palette preview · mirror flip do sticker (`H`) · ripple click feedback (state pronto, render visual fica pra v1.1 — opcional).

## Antes de tudo

1. Baixa [Iosevka release](https://github.com/be5invis/Iosevka/releases) — pega `PkgTTF-Iosevka-XX.X.X.zip`.
2. Extrai e copia `Iosevka-Regular.ttf` (ou `IosevkaTerm-Regular.ttf`) pra `coomer/Resources/Iosevka.ttf`.
3. Sem isso, builda mas crasha ao abrir overlay (FileNotFoundException no IosevkaFont.cs).

## Arquivos novos

Criar empty .cs no path indicado, copiar conteudo do .cs.txt do lado:

| Local                                                       | Conteudo                                                |
|-------------------------------------------------------------|---------------------------------------------------------|
| `coomer/Resources/Iosevka.ttf`                              | (TTF baixado, ver passo acima)                          |
| `coomer/Features/Text/IosevkaFont.cs`                       | `Features/Text/IosevkaFont.cs.txt`                      |
| `coomer/Features/Text/TextRenderer.cs`                      | `Features/Text/TextRenderer.cs.txt`                     |
| `coomer/Features/Hud/HudRenderer.cs`                        | `Features/Hud/HudRenderer.cs.txt`                       |
| `coomer/Features/Stickers/StickerPalette.cs`                | `Features/Stickers/StickerPalette.cs.txt`               |
| `coomer/Features/Effects/RippleEffect.cs`                   | `Features/Effects/RippleEffect.cs.txt`                  |
| `coomer/Shaders/text.vert.glsl`                             | `Shaders/text.vert.glsl.txt`                            |
| `coomer/Shaders/text.frag.glsl`                             | `Shaders/text.frag.glsl.txt`                            |

## Arquivos modificados

### Substituir completamente (paste-replace)

| Arquivo                                          | Conteudo                                              |
|--------------------------------------------------|-------------------------------------------------------|
| `coomer/coomer.csproj`                           | `coomer/coomer.csproj.txt`                            |
| `coomer/Features/Drawing/StickerStamp.cs`        | `Features/Drawing/StickerStamp.cs.txt`                |
| `coomer/Features/Stickers/StickerRenderer.cs`    | `Features/Stickers/StickerRenderer.cs.txt`            |

### Patches pontuais (so ler o .patch.txt e aplicar as edits ali):

| Arquivo                                       | Patch                                                 |
|-----------------------------------------------|-------------------------------------------------------|
| `coomer/Features/Drawing/DrawTool.cs`         | `Features/Drawing/DrawTool.cs.patch.txt`              |
| `coomer/Features/Input/InputHandler.cs`       | `Features/Input/InputHandler.cs.patch.txt`            |
| `coomer/Features/Rendering/Renderer.cs`       | `Features/Rendering/Renderer.cs.patch.txt`            |
| `coomer/App/CoomerApp.cs`                     | `App/CoomerApp.cs.patch.txt`                          |

## Como cada feature funciona

### Iosevka + TextRenderer

`IosevkaFont` lê TTF embutido no `.csproj` e cache `Font` por tamanho. `TextRenderer` rasteriza string em bitmap via `System.Drawing.Common`, upload pra GL texture, cache LRU de 96 strings (`MaxCache`). Cada `NewFrame()` incrementa o contador pra LRU evict. `Draw(text, sizePx, pos, windowSize, color)` desenha em screen-space.

### HUD

Top-right da tela. Texto unico que descreve o modo ativo:

- Nada ligado: HUD nao aparece.
- Draw mode: `DRAW · seta · vermelho · 4px` (+ ` · OCULTO` se `V` foi apertado)
- Stamp mode: `STAMP # · proximo: 3 · raio: 24px`
- Sticker mode: `STICKER · cute/laço.png · 128px` ou `STICKER · <-> laço.png · 128px` se mirror ligado
- Picker: `PICK · click num pixel` ou `PICK · ultimo: #AABBCC`
- Region copy: `REGION COPY · arrasta pra selecionar`

Toggle `F1` (futuro — fica visivel por padrao).

Halo: 4 deslocamentos do texto preto + texto branco por cima = legibilidade em qualquer fundo.

### Sticker palette preview

Bottom-center da tela. 7 thumbnails do que ta ao redor do sticker ativo na categoria atual. Atual destaca com opacity 1.0; vizinhos 0.55. Acima: nome da categoria. Abaixo: nome do arquivo ativo.

So aparece em sticker mode.

### Mirror flip (`H`)

Toggle do `DrawTool.StickerMirror`. Aplica na UV do quad do sticker — `u0/u1` trocam dependendo da flag. Cada sticker pousado guarda seu MirrorH proprio no `StickerStamp` (o estado do toggle no momento do click), entao voce pode flippar pra esquerda, pousar 1, flippar pra direita, pousar outro.

### Ripple feedback

`RippleEffect.Pop(pos)` registra um "click" — center + ttl=0.45s. `Tick(dt)` decrementa. State expoe `CurrentRadius` e `CurrentAlpha`.

Por enquanto so o STATE ta wireado. O RENDER visual (anel expandindo) eh fritura pra v1.1 do batch — seria um draw de circulo no StrokeRenderer em screen-space. Skippei pra nao explodir o tamanho desta leva.

## Como testar

1. Coloca o Iosevka.ttf em `coomer/Resources/Iosevka.ttf`.
2. `dotnet build coomer`.
3. Mata o coomer rodando, republica, abre overlay.
4. **HUD**: aperta `D` → HUD aparece no canto superior direito mostrando o modo.
5. **Palette**: `Y` → ve a faixa de stickers no rodape. `Tab` cicla, ve eles deslizarem.
6. **Mirror**: em sticker mode, `H` flippa. HUD mostra `<->`. Solta um sticker, `H` de novo, solta outro — vc tem dois espelhos diferentes.
7. **Ripple state**: nao tem visual ainda mas state ta vivo (`_ripple.Active` retorna true por 450ms apos click).

## Pendencias pra Leva 2

- Click num sticker existente seleciona ele
- Drag move o selecionado
- Wheel sobre selecionado redimensiona ele
- `Q`/`E` rotaciona (StickerStamp.Rotation + vert shader matrix)
- `Delete` remove selecionado
- Ripple visual aparecendo (ring expansivel no StrokeRenderer)
