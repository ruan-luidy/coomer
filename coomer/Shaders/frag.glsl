#version 130
out mediump vec4 color;
in mediump vec2 texcoord;
uniform sampler2D tex;
uniform vec2 cursorPos;
uniform vec2 windowSize;
uniform float flShadow;
uniform float flRadius;
uniform float cameraScale;
uniform bool mirror;

// ====================== Fase 2: bolha ======================
uniform vec2 bubblePos;
uniform vec2 bubbleStretch;
uniform float bubbleSqueeze;
uniform bool flEnabled;
// ===========================================================

// ====================== Fase 3: blur ======================
uniform bool blurBackground;
uniform float backgroundBlurRadius;
uniform bool blurOutsideFl;
uniform float outsideFlBlurRadius;
// ==========================================================

// ====================== Fase 3: helper boxBlur ======================
// 9x9 = 81 taps, single-pass. Suficiente p/ efeito suave sem dois passes
// (sem FBO). Em 4K pode pesar — abaixar o radius via config ajuda.
vec4 boxBlur(vec2 uv, float radius)
{
    vec2 texel = radius / textureSize(tex, 0);
    vec4 acc = vec4(0.0);
    float n = 0.0;
    for (int j = -4; j <= 4; j++)
        for (int i = -4; i <= 4; i++)
        {
            acc += texture(tex, uv + vec2(float(i), float(j)) * texel * 0.25);
            n += 1.0;
        }
    return acc / n;
}
// =====================================================================

// ====================== Fase 2: helper bubbleDist ======================
// Distancia ate o centro, mas projetada em base (dir, perp): estica ao
// longo de Stretch e comprime perpendicular por Squeeze. Sem stretch
// vira length(p - center) — o circulo normal.
float bubbleDist(vec2 p, vec2 center, vec2 stretch, float squeeze)
{
    vec2 d = p - center;
    float sl = length(stretch);
    if (sl < 0.0001) return length(d);
    vec2 dir = stretch / sl;
    vec2 perp = vec2(-dir.y, dir.x);
    float along = dot(d, dir);
    float across = dot(d, perp);
    float kAlong = 1.0 - clamp(sl * 0.5, 0.0, 0.5);   // estica
    float kAcross = 1.0 + clamp(squeeze, 0.0, 0.5);   // comprime
    return length(vec2(along / kAlong, across * kAcross));
}
// ========================================================================

void main()
{
    vec2 effective_texcoord = texcoord;
    if (mirror) {
        effective_texcoord.x = 1 - effective_texcoord.x;
    }

    // ====================== Fase 3: amostra base ======================
    // Se blur_background ligado, o fundo INTEIRO ja vem borrado; ai dentro
    // da lanterna a gente reamostra nitido p/ enxergar o que estamos vendo.
    vec4 base = blurBackground
        ? boxBlur(effective_texcoord, backgroundBlurRadius)
        : texture(tex, effective_texcoord);
    // ===================================================================

    // ====================== Fase 2: centro/distancia da bolha ======================
    // flRadius e em pixels de TELA (nao multiplica por cameraScale), entao o circulo
    // mantem o mesmo tamanho mesmo com zoom — ideal pra destacar texto ao dar zoom.
    vec2 center = vec2(bubblePos.x, windowSize.y - bubblePos.y);
    float dist = bubbleDist(gl_FragCoord.xy, center, bubbleStretch, bubbleSqueeze);
    // ===============================================================================

    if (!flEnabled || flShadow < 0.001)
    {
        // Lanterna desligada -> imagem (base) sem sombra. Mantem compat com original:
        // se flShadow=0 e flEnabled=0, comportamento e identico a "color = texture(...)".
        color = base;
        return;
    }

    if (dist < flRadius)
    {
        // dentro da lanterna -> imagem nitida (mesmo que blur_background esteja on)
        color = blurBackground ? texture(tex, effective_texcoord) : base;
    }
    else
    {
        // ====================== Fase 3: anel fora da lanterna ======================
        // Quando blur_outside_flashlight=true e o fundo NAO esta borrado,
        // borramos so a parte que vai escurecer (otimizacao: nao processa
        // duas vezes quando blur_background ja foi feito).
        vec4 outside = (blurOutsideFl && !blurBackground)
            ? boxBlur(effective_texcoord, outsideFlBlurRadius)
            : base;
        // ===========================================================================
        color = mix(outside, vec4(0.0, 0.0, 0.0, 1.0), flShadow);
    }
}
