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

uniform vec2 bubblePos;
uniform vec2 bubbleStretch;
uniform float bubbleSqueeze;
uniform bool flEnabled;

uniform bool blurBackground;
uniform float backgroundBlurRadius;
uniform bool blurOutsideFl;
uniform float outsideFlBlurRadius;

uniform bool flFisheye;
uniform float fisheyeStrength;
uniform bool flClearGlass;
uniform float clearGlassZoom;

// Gaussian blur com pesos exp(-d2/(2sigma2)). samples teto em 8 pra nao
// explodir em telas grandes; sigma = radius*0.5 deixa a curva mais chata
// (pesos das bordas significativos), ai o blur aparece mesmo com o anel
// escurecido por cima.
vec4 gaussianBlur(vec2 uv, float radius)
{
    if (radius < 0.5) return texture(tex, uv);
    vec4 sum = vec4(0.0);
    float total = 0.0;
    vec2 texel = 1.0 / windowSize;
    int samples = int(clamp(radius * 0.5, 1.0, 8.0));
    float sigma = radius * 0.5;
    float twoSigSq = 2.0 * sigma * sigma;
    float stride = radius / float(samples);
    for (int y = -samples; y <= samples; y++) {
        for (int x = -samples; x <= samples; x++) {
            float dx = float(x);
            float dy = float(y);
            float w = exp(-(dx*dx + dy*dy) / twoSigSq);
            vec2 offset = vec2(dx, dy) * stride * texel;
            sum += texture(tex, uv + offset) * w;
            total += w;
        }
    }
    return sum / total;
}

// SDF da bolha (elipsoidal): negativo dentro, positivo fora, 0 na borda.
// Sem stretch, colapsa pra length(p-center) - radius (circulo simples).
float sdfBubble(vec2 p, vec2 center, float radius, vec2 stretch, float squeeze)
{
    vec2 d = p - center;
    float sl = length(stretch);
    if (sl < 0.0001) return length(d) - radius;
    vec2 dir = stretch / sl;
    vec2 perp = vec2(-dir.y, dir.x);
    float along = dot(d, dir);
    float across = dot(d, perp);
    float rA = radius * clamp(1.0 + sl, 0.5, 2.0);
    float rB = radius * clamp(1.0 - squeeze, 0.3, 1.5);
    float minR = min(rA, rB);
    return length(vec2(along / rA, across / rB)) * minR - minR;
}

// Normal do "domo" virtual sobre a tela. O gradient do SDF (dFdx/dFdy)
// da a direcao xy; o z e calculado pra que a normal aponte pra cima no
// centro do domo (sd = -thickness) e pros lados na borda (sd = 0).
vec3 domeNormal(float sd, float thickness)
{
    float dx = dFdx(sd);
    float dy = dFdy(sd);
    float nCos = max(thickness + sd, 0.0) / thickness;
    float nSin = sqrt(max(0.0, 1.0 - nCos * nCos));
    return normalize(vec3(dx * nCos, dy * nCos, nSin));
}

// Altura do domo no ponto: 0 na borda, thickness no centro, half-sphere.
float domeHeight(float sd, float thickness)
{
    if (sd >= 0.0) return 0.0;
    if (sd < -thickness) return thickness;
    float x = thickness + sd;
    return sqrt(thickness * thickness - x * x);
}

void main()
{
    vec2 effective_texcoord = texcoord;
    if (mirror) effective_texcoord.x = 1.0 - effective_texcoord.x;

    // amostra base do fundo (com ou sem blur global)
    vec4 base = blurBackground
        ? gaussianBlur(effective_texcoord, backgroundBlurRadius)
        : texture(tex, effective_texcoord);

    if (!flEnabled && flShadow < 0.001) {
        color = base;
        return;
    }

    vec2 frag = gl_FragCoord.xy;
    vec2 center = vec2(bubblePos.x, windowSize.y - bubblePos.y);
    float sd = sdfBubble(frag, center, flRadius, bubbleStretch, bubbleSqueeze);

    // anel ao redor da bolha: texturizado (possivelmente blurado) + sombra suave
    vec4 outsideTexture = (blurOutsideFl && !blurBackground)
        ? gaussianBlur(effective_texcoord, outsideFlBlurRadius)
        : base;
    float edgeAlpha = smoothstep(-2.0, 0.0, sd);
    vec4 bgColor = mix(outsideTexture, vec4(0.0), min(edgeAlpha, flShadow));

    // fora da bolha — entrega o anel direto
    if (sd >= 0.0) {
        color = bgColor;
        return;
    }

    // lanterna desligando (animacao do shadow indo a zero) — sem vidro,
    // so suaviza a transicao da textura interna pro anel.
    if (!flEnabled) {
        color = mix(base, bgColor, edgeAlpha);
        return;
    }

    // ============= modo fisheye =============
    // barrel quadratico: rSamp = rNorm * mix(1, rNorm, k).
    //   centro (rNorm=0): rSamp=0, derivada = 1-k -> magnificacao 1/(1-k)
    //   borda  (rNorm=1): rSamp=1, derivada = 1+k -> casa direitinho com o anel
    // sample direto da textura, sem rim/sheen: a imagem fica nitida ("clear").
    if (flFisheye) {
        vec2 d = frag - center;
        float r = length(d);
        float rNorm = clamp(r / max(flRadius, 0.0001), 0.0, 1.0);
        vec2 dir = (r > 0.0001) ? d / r : vec2(0.0);
        float k = clamp(fisheyeStrength, 0.0, 0.95);
        float rSamp = rNorm * mix(1.0, rNorm, k);
        vec2 sampleOffset = dir * rSamp * flRadius;
        vec2 displ = sampleOffset - d;
        if (mirror) displ.x = -displ.x;
        vec2 fishUV = effective_texcoord + displ / windowSize;
        color = mix(texture(tex, fishUV), bgColor, edgeAlpha);
        return;
    }

    // ============= modo vidro claro (lupa de leitura) =============
    // zoom uniforme em toda a bolha: sampleOffset = d / zoom. linha reta
    // continua reta em qualquer lugar — sem curvatura/warp perto da borda.
    // tem um pequeno salto no proprio rim (conteudo zoomado vs anel real),
    // mas ele cai dentro do fade do edgeAlpha + shadow do anel, fica invisivel.
    if (flClearGlass) {
        float zoom = max(1.0, clearGlassZoom);
        vec2 d = frag - center;
        vec2 displ = d / zoom - d;
        if (mirror) displ.x = -displ.x;
        vec2 glassUV = effective_texcoord + displ / windowSize;
        color = mix(texture(tex, glassUV), bgColor, edgeAlpha);
        return;
    }

    // ============= efeito de vidro: refracao + reflexao =============
    // o domo virtual tem "thickness" de altura sobre a tela. luz da camera
    // (z = -1) entra pela superficie curva e refrata com IOR 1.45 (vidro);
    // a projecao xy do raio refratado da a deslocacao do uv (lente).
    float thickness = 12.0 * pow(cameraScale, 0.5);
    float ior = 1.45;
    float baseHeight = thickness * 6.0;

    vec3 normal = domeNormal(sd, thickness);
    vec3 incident = vec3(0.0, 0.0, -1.0);
    vec3 refractVec = refract(incident, normal, 1.0 / ior);
    float h = domeHeight(sd, thickness);
    float refractLength = (h + baseHeight)
                        / max(0.001, dot(vec3(0.0, 0.0, -1.0), refractVec));
    vec2 refractOffset = refractVec.xy * refractLength;
    if (mirror) refractOffset.x = -refractOffset.x;
    vec2 refractedUV = effective_texcoord + refractOffset / windowSize;
    vec4 refractColor = texture(tex, refractedUV);

    // brilho refletivo na borda (mais forte onde a normal aponta pros lados)
    vec3 reflectVec = reflect(incident, normal);
    float sheen = clamp(abs(reflectVec.x - reflectVec.y), 0.0, 1.0);
    vec4 reflectColor = vec4(vec3(sheen), 0.0);
    float reflectionFactor = (1.0 - normal.z) * 0.2 * (thickness / 12.0);
    vec4 glassColor = clamp(mix(refractColor, reflectColor, reflectionFactor),
                            0.0, 1.0);

    color = mix(glassColor, bgColor, edgeAlpha);
}
