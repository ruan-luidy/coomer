# Roteiro — feature "drawing"

Modo de anotação: dentro do overlay, dá pra desenhar à mão livre (suavizado),
traçar linhas retas, setas, retângulos e círculos por cima da captura. Tudo
fica preso ao conteúdo da imagem (pan/zoom/mirror movem o desenho junto).

## Arquivos

- `Stroke.cs` — tipos: enum `DrawShape` (`Free`/`Line`/`Arrow`/`Rect`/`Circle`) + classe `Stroke`.
- `OneEuroFilter.cs` — filtro 1€ pra suavizar o cursor (mata tremedeira sem lag em movimento rápido).
- `DrawTool.cs` — estado e regras: paleta, espessura, ciclo de forma, undo/clear, e o detector de auto-círculo no `End()`.
- `StrokeRenderer.cs` — gera geometria por shape e desenha. Inclui o ringue do brush no cursor.
- `Shaders/stroke.vert.glsl` + `stroke.frag.glsl` — shader com AA por distance-field (`fwidth`).

Integração: `Renderer.Draw(...)` chama `StrokeRenderer.Draw(...)` depois do quad da screenshot.
`InputHandler` roteia o left-drag pro `DrawTool` quando o modo tá ligado.
`CoomerApp` instancia o `DrawTool` e plumba pra todo mundo.

## Controles (dentro do overlay)

| Tecla              | Ação                                                     |
|--------------------|----------------------------------------------------------|
| `D`                | Liga/desliga modo desenho (exclusivo com lanterna/picker)|
| `S`                | Cicla a forma: pen → linha → seta → retângulo → círculo  |
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

### Smoothing — 1€ Filter + Catmull-Rom
Duas camadas de suavização:

1. **1€ Filter** no cursor *antes* da conversão pra coord de imagem. Devagar
   corta tremor, rápido cede pra não atrasar. Default `MinCutoff=1.0`,
   `Beta=0.01`. Cada traço novo dá `Reset()` no filtro pra não puxar o início
   do novo da última posição do anterior.
2. **Catmull-Rom** na renderização: o pen passa uma spline pelos pontos
   amostrados em vez de ligar por retas. Tangência das pontas clampada nelas
   mesmas. Subdividido por comprimento (`segLen/3`, clamp [1,24]) — não tessela
   demais em traço longo. Custo zero de lag (é puramente geométrico).

### AA por distance-field
`stroke.frag.glsl` calcula `distSeg(fragment, A, B)` (distância ao segmento com
clamp `t∈[0,1]`) e usa `smoothstep(half-fwidth, half+fwidth)` pro alpha.
`fwidth(d)` te dá a derivada de `d` por pixel-de-tela, então a banda de AA
fica sempre ~2 pixels independente do zoom.

**Bônus de graça**: o clamp da distSeg vira **round caps** automáticos nas
pontas. Segmentos consecutivos do pen têm extremidades coincidentes → as duas
calotas redondas se fundem perfeitamente, sem agulha visível na junção.

### Espessura em px de imagem
Salva como px de imagem (não px de tela). Resultado: dar zoom *engrossa* o traço
junto com o resto, como se fosse tinta na página — não o traço fininho de uma
UI 2D que fura o zoom. Quem quiser comportamento de UI muda dividindo por
`cameraScale` no shader.

### Ringue indicador do brush
Quando o modo desenho tá ligado, o `StrokeRenderer` adiciona um círculo fino
no cursor com **diâmetro = espessura atual em pixels-de-imagem** (ou seja, exatamente
o que o pincel vai pintar). A espessura do ringue em si é escalada por
`1/cameraScale` pra ficar sempre ~1 pixel-de-tela. Cor: cor atual do brush
em alpha 0.75.

### Render no topo de tudo
O `StrokeRenderer.Draw(...)` roda *depois* do quad da screenshot no
`Renderer.Draw`. Então traços ficam visíveis mesmo dentro da sombra da
lanterna — comportamento útil pra usar lanterna + anotação juntas.

### Sem persistência
Strokes vivem no `DrawTool` que é criado a cada `CoomerApp.Run()`. Fecha o
overlay, perde os traços. Faz sentido porque a screenshot também é nova a cada
abertura — desenho ligado a uma captura específica.

## Shapes

- **Free** (pen): polilinha suavizada (1€ no input + Catmull-Rom no render).
- **Line**: 2 pontos, segmento reto. Preview em tempo real durante o drag.
- **Arrow**: 2 pontos. Shaft `a→b` + cabeça em V em `b` (ponta = onde solta o
  mouse). Cabeça com abertura de ~28°, comprimento clampado em
  `[thickness*6, len*0.5]` pra não sumir em seta curta nem virar flecha medieval
  em seta gigante.
- **Rect**: 2 pontos = cantos opostos. 4 segmentos (contorno só).
- **Circle**: 2 pontos. `Points[0]` = centro (clique), `Points[1]` = ponto na
  borda (drag). Raio = distância. Polígono regular com 40–128 segmentos
  (escala com o raio pra não virar octógono em círculo grande).

### Auto-círculo (estilo iPhone Scribble)

Se o usuário desenha um rabisco *fechado* e *redondo o bastante* no modo
`Free`, ao soltar o mouse ele vira um círculo perfeito. Heurística em
`DrawTool.TryDetectCircle`, rodada no `End()`. Critérios em ordem (qualquer
falha → não é círculo, mantém o rabisco):

1. **≥16 pontos amostrados** — traço curto demais é rabisco curto, não círculo.
2. **Raio médio ≥ 12 px** — círculo minúsculo é tap/jitter.
3. **stddev(raio) / média(raio) ≤ 0.22** — todos os pontos a distância parecida
   do centroide (forma circular vs. oval/quadrada).
4. **Quase fechado**: `dist(ponto0, pontoN) ≤ 0.6 × raioMédio`.
5. **Cobertura angular ≥ 306°** (cumulativa, com sinal): evita "C aberto"
   passar como círculo.

Centro = centroide dos pontos; raio = distância média ao centroide. Pra
desabilitar geral: `DrawTool.AutoCircle = false`. Pra ajustar sensibilidade,
tweak os thresholds em `TryDetectCircle`.

## Como testar

1. `Ctrl+Alt+Z` pra abrir overlay.
2. Aperta `D` → modo desenho ligado. Cursor visível + ringue do brush no cursor.
3. Segura botão esquerdo e arrasta → traço suavizado.
4. `S` → linha. `S` de novo → seta. De novo → retângulo. De novo → círculo. De novo → volta pro pen.
5. No modo pen, desenha um rabisco redondo e fechado → solta o mouse → vira círculo perfeito.
6. `[` / `]` ou `Ctrl+scroll` muda grossura (vê o ringue do brush crescer/diminuir).
7. `,` muda cor. `Z` desfaz. `X` limpa.
8. Dá zoom (`scroll`), o desenho escala junto. Pan: usa H/J/K/L (drag do esquerdo tá usado pelo pincel) ou desliga o modo desenho.
9. `M` espelha — desenho espelha junto.
10. `F` lanterna — traços continuam visíveis acima da sombra.
