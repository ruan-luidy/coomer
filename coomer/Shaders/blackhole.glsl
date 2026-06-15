#version 130
// black hole — Schwarzschild lens tracada por geodesica.
//
// Adaptado de github.com/s0xDk/ghostty-blackhole (MIT, (c) 2026 s13k),
// inspirado por sua vez no shader de Eric Bruneton
// (https://ebruneton.github.io/black_hole_shader/). Texto MIT completo em
// THIRD_PARTY_LICENSES.md na raiz do repo.
//
// Cada pixel integra a geodesica nula do foton numericamente:
//   a = -(3/2) h^2 x / r^5   (forma de Binet, reproduz a curvatura exata)
// e tudo cai dessa integracao em vez de ser pintado:
//   * a sombra            — raios com b < b_crit = (3sqrt3/2) r_s caem no horizonte
//   * lente gravitacional — raios que escapam sao projetados de volta no "ceu"
//                           (aqui o ceu e o screenshot: texto/janela dobra,
//                           amplia e espelha dentro do anel de Einstein)
//   * photon ring         — raios que enrolam perto da esfera de fotons (1.5 r_s)
//   * disco de acrecao    — disco fino Kepleriano que o raio cruza varias vezes;
//                           cor blackbody (Shakura-Sunyaev) com shift/beaming
//                           relativistico g = sqrt(1 - 1.5/r)/(1 - beta.k)
//
// Diferencas do original Ghostty: sem os modos token/pomodoro/demo e sem o
// decode de cor de cursor (aquilo era especifico do terminal). Aqui o efeito
// e ligado/desligado pela tecla G; `intensity` (0..1) faz o crescimento
// animado ao ligar e dirige lente/disco/dilatacao temporal. As fontes mudam:
//   iChannel0 -> tex (o screenshot)   iResolution -> windowSize   iTime -> iTime
// Unidades: r_s (raio de Schwarzschild) = 1.

out vec4 color;
in vec2 texcoord;

uniform sampler2D tex;
uniform vec2 windowSize;
uniform float iTime;
uniform float intensity; // 0..1 — mestre (cresce ao ligar)
uniform bool mirror;

// ----------------- uniforms vindos da CPU (preset + interacao) -------------
uniform float uHoleScale;   // multiplica HOLE_RADIUS (ctrl+scroll); inclui pulse
uniform vec2  uCenter;      // override do centro em [0,1]; (-1,-1) = use Lissajous
uniform float uDiskIncl;    // rad, vem do preset (ou [/]/]/ override)
uniform float uDiskTemp;    // K
uniform float uDopplerMix;  // 0..1
uniform float uDiskGain;
uniform float uDiskBeam;
uniform float uDriftSpeed;
uniform float uDilationMin;

// ---------------------------------------------------------------- tunables --
const float HOLE_RADIUS   = 0.0400; // raio da sombra (fracao da altura) em intensidade plena
const float LENS_DEPTH    = 13.0000; // distancia ate o plano "ceu" (screenshot), em r_s
// geometria do disco (raios em r_s)
const float DISK_INNER    = 1.8000; // borda interna (3 r_s = ISCO, ultima orbita estavel)
const float DISK_OUTER    = 8.0000; // borda externa
const float DISK_ROLL     = 0.3500; // rotacao do sistema no plano da tela, rad
// materia & luz do disco
const float DISK_OPACITY  = 0.9000; // quanto o disco proximo esconde o que ta atras (0..1)
const float DISK_SPEED    = 5.0000; // velocidade do padrao de streaks; negativo inverte a orbita
const float DISK_WIND     = 7.0000; // aperto do espiralamento das streaks
const float DISK_CONTRAST = 1.6000; // contraste das streaks: 0 = nevoa lisa, maior = filamentos
const float EXPOSURE      = 1.4000; // exposicao do tonemap da luz do disco

// passos da integracao da geodesica por pixel (so pixels perto do buraco pagam)
#define N_STEPS 48

// parametro de impacto critico de Schwarzschild, em r_s: raios abaixo caem;
// e o raio aparente (sombra) visto de longe. Fisica, nao gosto.
#define B_CRIT 2.5980762

// ------------------------------------------------------------------- noise --
float hash21(vec2 p) {
    p = fract(p * vec2(234.34, 435.345));
    p += dot(p, p + 34.23);
    return fract(p.x * p.y);
}

// value noise cujo eixo y enrola a cada perY celulas — usado na dimensao
// angular do disco pras streaks ladrilharem sem costura no corte do atan
float vnoiseWrapY(vec2 p, float perY) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    float y0 = mod(i.y, perY), y1 = mod(i.y + 1.0, perY);
    return mix(mix(hash21(vec2(i.x, y0)),       hash21(vec2(i.x + 1.0, y0)), f.x),
               mix(hash21(vec2(i.x, y1)),       hash21(vec2(i.x + 1.0, y1)), f.x),
               f.y);
}

// mirrored repeat mantem amostras lensadas na tela sem borrar a borda
vec2 mirrorUV(vec2 u) { return 1.0 - abs(1.0 - mod(u, 2.0)); }

vec2 rot(vec2 v, float a) {
    float c = cos(a), s = sin(a);
    return vec2(c * v.x - s * v.y, s * v.x + c * v.y);
}

// vagar Lissajous unitario: 2+2 senos incomensuraveis por eixo, nunca repete
vec2 lissa(float t) {
    return vec2(0.75 * sin(t * 0.37) + 0.25 * sin(t * 0.83 + 1.0),
                0.70 * sin(t * 0.54 + 2.1) + 0.30 * sin(t * 1.07));
}

// cor blackbody pela temperatura em Kelvin (fit de Tanner Helland, normalizado)
vec3 blackbody(float T) {
    float t = clamp(T, 1500.0, 40000.0) / 100.0;
    float r = t <= 66.0 ? 1.0
                        : clamp(1.292936 * pow(t - 60.0, -0.1332047), 0.0, 1.0);
    float g = t <= 66.0 ? clamp(0.3900816 * log(t) - 0.6318414, 0.0, 1.0)
                        : clamp(1.1298909 * pow(t - 60.0, -0.0755148), 0.0, 1.0);
    float b = t >= 66.0 ? 1.0
                        : (t <= 19.0 ? 0.0
                                     : clamp(0.5432068 * log(t - 10.0) - 1.1962540, 0.0, 1.0));
    return vec3(r, g, b);
}

// amostra do screenshot (o "ceu"), com o mesmo flip de mirror do shader base
vec4 sampleScene(vec2 u) {
    if (mirror) u.x = 1.0 - u.x;
    return texture(tex, u);
}

// ------------------------------------------------------------------- image --
void main() {
    vec2  res    = windowSize;
    vec2  uv     = texcoord;
    float aspect = res.x / res.y;

    float t = iTime * uDriftSpeed;

    // ---- driver: intensidade mestre I, tamanho sz e centro de deriva ----
    // intensity (0..1) vem da CPU: cresce ao ligar a tecla G e cai ao desligar.
    float lvl = clamp(intensity, 0.0, 1.0);
    float vis = smoothstep(0.0, 0.04, lvl);
    if (vis <= 0.0) { color = sampleScene(uv); return; }

    float I  = mix(0.10, 1.0, lvl);  // lente/disco/dilatacao seguem a intensidade
    float sz = mix(0.35, 1.0, lvl);  // sombra cresce de pequena pra cheia

    // deriva preguicosa pela tela, confinada pra ficar sempre visivel
    // (a CPU pode sobrescrever via uCenter — drag do mouse no modo interactive)
    vec2 center = (uCenter.x >= 0.0)
        ? uCenter
        : vec2(0.5, 0.5) + lissa(t * 0.25) * vec2(0.16, 0.18);

    float rh = HOLE_RADIUS * sz * uHoleScale; // raio em unidades de tela

    // dilatacao temporal gravitacional: buraco mais pesado desacelera o disco
    float dil = mix(1.0, uDilationMin, I);

    // o deslocamento (nao a cor) some longe do buraco, pra nao tremer a tela
    // toda enquanto ele deriva — entao nao ha costura visivel
    float shield = vis;

    // disco saneado: borda interna fora da esfera de fotons (1.5 r_s)
    float rin  = max(DISK_INNER, 1.6);
    float rout = max(DISK_OUTER, rin + 0.5);

    // frame corrigido por aspecto, centrado no buraco (y em alturas de tela)
    vec2  p    = (uv - center) * vec2(aspect, 1.0);
    float plen = length(p);

    // mapa tela <-> mundo: a sombra tem tamanho angular B_CRIT r_s e queremos
    // ela com rh de largura, entao 1 unidade de tela = W raios de Schwarzschild
    float W  = B_CRIT / max(rh, 1e-4);
    vec2  pr = rot(vec2(p.x, -p.y), DISK_ROLL) * W;
    float b  = length(pr);           // parametro de impacto do raio, em r_s

    // janela de distancia: a lente real cai como 1/b e tremeria a tela toda;
    // apaga umas poucas diametros de disco pra fora (deliberadamente nao-fisico)
    float window = exp(-pow(plen / (7.0 * rh), 2.0));

    float bmax = rout + 3.0;         // raios alem disso nao tocam o disco
    float Z0   = max(14.0, rout + 5.0); // distancia da camera (igual a do tracer)

    // ================= campo distante: deflexao fraca analitica ==============
    // mesma projecao de camera finita ajustada contra o integrador (sub-1% na
    // fronteira): evita o salto circular de ~20% no handoff.
    if (b >= bmax) {
        float u    = Z0 * inversesqrt(Z0 * Z0 + b * b);
        float defl = (2.0 / (W * W)) / max(plen, 1e-4)
                   * (1.29 * u + 0.07) * max(LENS_DEPTH - 2.14 * u + 0.75, 0.0)
                   * window * shield;
        vec2  dir  = p / max(plen, 1e-5);
        vec3  term;
        // leve aberracao cromatica: azul dobra um tico mais que vermelho
        float ab = 0.035 * smoothstep(1.0, 2.0, b / bmax);
        for (int i = 0; i < 3; i++) {
            float k   = 1.0 + (float(i) - 1.0) * ab;
            vec2  sp  = p - dir * defl * k;
            vec2  suv = mirrorUV(center + sp / vec2(aspect, 1.0));
            vec4  sc  = sampleScene(suv);
            term[i]   = sc[i];
        }
        color = vec4(term, 1.0);
        return;
    }

    // ====================== campo proximo: traca a geodesica =================
    // raios paralelos de uma camera distante em +z. buraco na origem, r_s = 1.
    // integra  x'' = -(3/2) h^2 x / r^5  (bending exato; h = |x x v| conservado)
    vec3  x  = vec3(pr, Z0);
    vec3  v  = vec3(0.0, 0.0, -1.0);
    float h2 = dot(pr, pr);

    // plano do disco: normal inclinada uDiskIncl no eixo x da tela
    float ci = cos(uDiskIncl), si = sin(uDiskIncl);
    vec3  n  = vec3(0.0, si, ci);
    vec3  e2 = vec3(0.0, ci, -si);   // eixo no plano completando (x, e2, n)
    float sdir = DISK_SPEED < 0.0 ? -1.0 : 1.0;
    float dspd = abs(DISK_SPEED);

    vec3  emitc = vec3(0.0);         // luz do disco acumulada (HDR)
    float trans = 1.0;               // transmitancia ate o fundo
    bool  captured = false;
    float sPrev = dot(x, n);
    vec3  xPrev = x;

    for (int i = 0; i < N_STEPS; i++) {
        float r2 = dot(x, x);
        if (r2 < 1.0) { captured = true; break; }     // atravessou o horizonte
        if (x.z < -Z0 && v.z < 0.0) break;            // escapou pra tras
        if (r2 > 4.0 * Z0 * Z0) break;                // jogado longe pro lado
        float r  = sqrt(r2);
        // passo escala com o raio: fino perto da esfera de fotons, grosso longe
        float dt = clamp(0.16 * r, 0.03, 1.5);
        // leapfrog (kick-drift-kick) mantem as orbitas quase-criticas estaveis
        vec3 a = -1.5 * h2 * x / (r2 * r2 * r);
        v += a * (0.5 * dt);
        x += v * dt;
        r2 = dot(x, x);
        r  = sqrt(r2);
        a  = -1.5 * h2 * x / (r2 * r2 * r);
        v += a * (0.5 * dt);

        // ---- cruzamento do disco fino: o raio perfurou o plano ----
        // gain=0 (preset pure lens) desliga o disco inteiro: nem emite nem
        // bloqueia o fundo, entao so resta sombra + lente gravitacional.
        float s = dot(x, n);
        if (uDiskGain > 1e-4 && s * sPrev < 0.0 && trans > 0.02) {
            float tc = sPrev / (sPrev - s);
            vec3  xc = mix(xPrev, x, tc);
            float rc = length(xc);
            if (rc > rin && rc < rout) {
                float band = smoothstep(rin, rin * 1.25, rc)
                           * (1.0 - smoothstep(rout * 0.70, rout, rc));

                // polares no plano do disco pra textura de streaks
                float phi   = atan(dot(xc, e2), xc.x);
                float turns = phi / 6.2831853;
                float kep   = pow(rin / rc, 1.5);
                // sqrt(1 - 1.5/r): tempo corre mais devagar nas orbitas internas
                float gloc  = sqrt(max(1.0 - 1.5 / rc, 0.02));
                float swirl = rc * DISK_WIND * 0.12 - t * kep * dspd * gloc * dil * sdir;
                float streaks = vnoiseWrapY(vec2(rc * 2.8, turns * 19.0 + swirl * 3.0), 19.0) * 0.65 +
                                vnoiseWrapY(vec2(rc * 1.0, turns * 9.0  + swirl * 1.5 + 7.0), 9.0) * 0.35;
                streaks = 0.35 + DISK_CONTRAST * streaks * streaks;

                // Doppler relativistico + shift gravitacional pro gas em orbita
                // circular: g = sqrt(1 - 1.5/r) / (1 - beta.k)
                vec3  gasdir = normalize(cross(n, xc)) * sdir;
                float beta   = clamp(inversesqrt(max(2.0 * (rc - 1.0), 0.2)), 0.0, 0.99);
                float g      = gloc / max(1.0 + beta * dot(gasdir, normalize(v)), 0.05);
                g = mix(1.0, g, uDopplerMix);

                // perfil de temperatura Shakura-Sunyaev, pico normalizado em 1
                float xpr   = max(1.0 - sqrt(rin / rc), 0.0);
                float tprof = pow(rin / rc, 0.75) * pow(xpr, 0.25) / 0.488;
                vec3  cbb   = blackbody(uDiskTemp * tprof * g); // cor com doppler
                float boost = pow(g, uDiskBeam);                // beaming

                float density = band * streaks;
                emitc += trans * cbb * (uDiskGain * 2.2 * density * tprof * tprof * boost);
                trans *= 1.0 - clamp(DISK_OPACITY * density, 0.0, 1.0);
            }
        }
        sPrev = s;
        xPrev = x;
    }
    // raios ainda enrolados na esfera de fotons quando o budget acabou: capturados
    if (!captured && dot(x, x) < 4.0) captured = true;

    // ---- fundo: de onde veio o raio que escapou? ----
    vec3 bg = vec3(0.0);
    if (!captured) {
        vec3 d = normalize(v);
        if (d.z < -0.05) {
            // projeta o raio reto de saida no plano "ceu" em z = -LENS_DEPTH
            float tpl = (-LENS_DEPTH - x.z) / d.z;
            vec3  hp  = x + d * tpl;
            vec2  q   = rot(hp.xy, -DISK_ROLL) / W;
            vec2  sp  = vec2(q.x, -q.y);
            // so o deslocamento e apagado por window/shield, nunca a cor —
            // um warp continuo nao deixa costura
            vec2  suv = mirrorUV(center + (p + (sp - p) * window * shield) / vec2(aspect, 1.0));
            // raios dobrados alem de ~90 graus nunca alcancam o ceu atras do
            // buraco; somem pro preto em vez de amostrar lixo
            float toward = smoothstep(0.05, 0.35, -d.z);
            bg += sampleScene(suv).rgb * toward;
        }
    }

    // luz do disco e HDR; tonemapeia por cima da amostra do screenshot
    vec3 col = bg * trans + (vec3(1.0) - exp(-emitc * EXPOSURE));
    color = vec4(col, 1.0);
}
