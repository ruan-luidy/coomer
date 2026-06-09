# Roteiro — feature "drawing"

Modo de anotação: dentro do overlay, dá pra desenhar à mão livre, traçar linhas
retas e marcar retângulos por cima da captura. Tudo fica preso ao conteúdo da
imagem (pan/zoom/mirror movem o desenho junto).

## Arquivos a colar

Cada `.cs.txt` (e `.glsl.txt`) tem o conteúdo final do `.cs`/`.glsl` de mesmo nome
na mesma pasta. Cola por cima, salva, dotnet build.

### Novos (`.cs` está vazio, conteúdo em `.cs.txt`)

| Vazio                                                       | Conteúdo                                                         |
|-------------------------------------------------------------|------------------------------------------------------------------|
| `coomer/Features/Drawing/Stroke.cs`                         | `coomer/Features/Drawing/Stroke.cs.txt`                          |
| `coomer/Features/Drawing/OneEuroFilter.cs`                  | `coomer/Features/Drawing/OneEuroFilter.cs.txt`                   |
| `coomer/Features/Drawing/DrawTool.cs`                       | `coomer/Features/Drawing/DrawTool.cs.txt`                        |
| `coomer/Features/Drawing/StrokeRenderer.cs`                 | `coomer/Features/Drawing/StrokeRenderer.cs.txt`                  |
| `coomer/Shaders/stroke.vert.glsl`                           | `coomer/Shaders/stroke.vert.glsl.txt`                            |
| `coomer/Shaders/stroke.frag.glsl`                           | `coomer/Shaders/stroke.frag.glsl.txt`                            |

Os shaders novos entram no `.csproj` automaticamente — o item
`<EmbeddedResource Include="Shaders\*.glsl" />` já é wildcard.

### Modificados (substituir o `.cs` existente pelo conteúdo do `.cs.txt` ao lado)

| Arquivo `.cs`                                  | Conteúdo final em                                    | O que mudou                                                              |
|------------------------------------------------|------------------------------------------------------|--------------------------------------------------------------------------|
| `coomer/Features/Rendering/Shader.cs`          | `coomer/Features/Rendering/Shader.cs.txt`            | Ganhou `SetVec4(name, Vector4)` pra cor dos traços.                      |
| `coomer/Features/Rendering/Renderer.cs`        | `coomer/Features/Rendering/Renderer.cs.txt`          | Instancia `StrokeRenderer`, `Draw(...)` agora aceita `DrawTool`.         |
| `coomer/Features/Input/InputHandler.cs`        | `coomer/Features/Input/InputHandler.cs.txt`          | Recebe `DrawTool`, teclas novas (D/S/Z/X/[/]/,), roteia mouse quando desenhando, Ctrl+scroll = espessura. |
| `coomer/App/CoomerApp.cs`                      | `coomer/App/CoomerApp.cs.txt`                        | Cria `DrawTool` e passa pro `Renderer`/`InputHandler`.                   |

## Controles novos (dentro do overlay)

| Tecla              | Ação                                                     |
|--------------------|----------------------------------------------------------|
| `D`                | Liga/desliga modo desenho (exclusivo com lanterna/picker)|
| `S`                | Cicla a forma: pen → linha → retângulo                   |
| `Z`                | Desfaz o último traço                                    |
| `X`                | Limpa tudo                                               |
| `[` / `]`          | Espessura menor / maior                                  |
| `,`                | Cicla a cor (vermelho/amarelo/verde/azul/magenta/branco/preto) |
| `Ctrl + scroll`    | Ajuste fino da espessura                                 |
| Click esquerdo     | Desenha (em vez de panar — só com o modo ligado)         |

Sair do modo é `D` de novo. `Esc`/`Q` continuam fechando o overlay inteiro.

## Decisões de design

### Coords em pixel da imagem
Os traços guardam pontos em coords da screenshot (`Vector2` em pixels da
captura), não em coords de tela. Vantagem: o vértice usa o **mesmo transform do
shader da screenshot** (vert.glsl), então pan/zoom/mirror reusam a mesma matemática
sem retrabalho. O `DrawTool.ScreenToImage` faz a conversão na entrada (clique
e move), incluindo o "desfaz mirror" pra guardar coords canônicas.

### Smoothing — 1€ Filter
Filtro `OneEuroFilterV2` aplicado ao cursor *antes* da conversão pra coord de
imagem. É o padrão da indústria pra suavizar input de mouse/stylus em UI/desenho:

- Devagar (`speed≈0`) → cutoff baixo → corta tremor agressivo.
- Rápido → cutoff alto → quase passthrough, sem lag.

Parâmetros: `MinCutoff=1.0`, `Beta=0.01`. Se você quiser tremor zero, baixa
`MinCutoff` pra `0.5`; se sentir lag, sobe pra `1.5–2.0`. Cada traço novo dá
`Reset()` no filtro pra não puxar o início do novo da última posição do anterior.

Sample-rate adapter: o `Stopwatch` interno mede o `dt` real entre eventos do
mouse (que vêm a centenas de Hz). Sem isso o filtro não calibra direito.

### Espessura em px de imagem
Salva como px de imagem (não px de tela). Resultado: dar zoom *engrossa* o traço
junto com o resto, como se fosse tinta na página — não o traço fininho de uma
UI 2D que fura o zoom. Quem quiser comportamento de UI muda dividindo por
`cameraScale` no shader.

### Render no topo de tudo
O `StrokeRenderer.Draw(...)` roda *depois* do quad da screenshot no
`Renderer.Draw`. Então traços ficam visíveis mesmo dentro da sombra da
lanterna — comportamento útil pra usar lanterna + anotação juntas.

### Geometria simples (sem miter)
Cada segmento vira um quad com **cap +halfWidth** nas duas extremidades. Pares
de segmentos consecutivos sobrepõem por `h` no encontro — esconde a juntura
sem precisar calcular miter. Pra traços com pouca curvatura (que é o caso
quando o 1€ tá ligado) funciona indistinguível de bevel/miter. Se um dia rolar
querer traços muito mais grossos com curva fechada, dá pra trocar pra
triangle-strip com miter clamp.

### Sem persistência
Strokes vivem no `DrawTool` que é criado a cada `CoomerApp.Run()`. Fecha o
overlay, perde os traços. Faz sentido porque a screenshot também é nova a cada
abertura — desenho ligado a uma captura específica.

## Como testar

1. Cola tudo, `dotnet build coomer`.
2. `dotnet run --project coomer` (ou roda o `coomer.exe` se já tava residente, mata e reabre).
3. `Ctrl+Alt+Z` pra abrir overlay.
4. Aperta `D` → modo desenho ligado. Cursor visível, segura botão esquerdo e arrasta.
5. `S` → vira modo linha. Click + drag → linha reta de A pra B (preview em tempo real).
6. `S` de novo → retângulo (mesmo padrão).
7. `[` / `]` ajusta grossura. `,` muda cor. `Z` desfaz, `X` limpa.
8. Dá zoom (`scroll`), o desenho escala junto. Pan (drag tem que ser COM modo desenho desligado, ou usa H/J/K/L) também leva o desenho junto.
9. `M` espelha — desenho espelha junto.
10. `F` lanterna — traços continuam visíveis acima da sombra.
