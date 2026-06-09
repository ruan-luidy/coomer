#version 130
in vec2 aPos;
in vec2 aSegA;
in vec2 aSegB;
in float aHalfWidth;

out vec2 vPosImg;
out vec2 vA;
out vec2 vB;
out float vHalf;

uniform vec2 cameraPos;
uniform float cameraScale;
uniform vec2 windowSize;
uniform vec2 screenshotSize;
uniform bool mirror;

// Mesma conta do vert.glsl da screenshot — mantem o desenho perfeitamente
// alinhado com a imagem em qualquer zoom/pan.
vec2 to_world(vec2 v) {
    vec2 ratio = vec2(
        windowSize.x / screenshotSize.x / cameraScale,
        windowSize.y / screenshotSize.y / cameraScale);
    return vec2((v.x / screenshotSize.x * 2.0 - 1.0) / ratio.x,
                (v.y / screenshotSize.y * 2.0 - 1.0) / ratio.y);
}

void main()
{
    // aPos vem em coord canonica (sem mirror) Y-down (Y=0 topo da foto). Pro
    // posicionamento no clip, refazemos o mirror e o flip pra Y-up (o screenshot
    // quad faz isso indiretamente via textura BGRA top-down).
    vec2 p = aPos;
    if (mirror) p.x = screenshotSize.x - p.x;
    p.y = screenshotSize.y - p.y;
    gl_Position = vec4(to_world(p - cameraPos * vec2(1.0, -1.0)), 0.0, 1.0);

    // Pro AA, a frag mede distancia em coord canonica nao-flipada — entao
    // passamos os 3 sem mexer. fwidth(distSeg) cuida da escala/zoom.
    vPosImg = aPos;
    vA = aSegA;
    vB = aSegB;
    vHalf = aHalfWidth;
}
