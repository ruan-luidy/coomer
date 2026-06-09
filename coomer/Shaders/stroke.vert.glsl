#version 130
in vec2 aPos;

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
    // Pontos foram guardados em coords canonicas da imagem (sem mirror); aqui
    // reaplicamos o flip pra acompanhar a renderizacao espelhada da screenshot.
    vec2 p = aPos;
    if (mirror) p.x = screenshotSize.x - p.x;
    gl_Position = vec4(to_world(p - cameraPos * vec2(1.0, -1.0)), 0.0, 1.0);
}
