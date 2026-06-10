#version 130
in vec2 aPos;
in vec2 aUv;

out vec2 vUv;

uniform vec2 cameraPos;
uniform float cameraScale;
uniform vec2 windowSize;
uniform vec2 screenshotSize;
uniform bool mirror;

vec2 to_world(vec2 v) {
    vec2 ratio = vec2(
        windowSize.x / screenshotSize.x / cameraScale,
        windowSize.y / screenshotSize.y / cameraScale);
    return vec2((v.x / screenshotSize.x * 2.0 - 1.0) / ratio.x,
                (v.y / screenshotSize.y * 2.0 - 1.0) / ratio.y);
}

void main()
{
    vec2 p = aPos;
    if (mirror) p.x = screenshotSize.x - p.x;
    p.y = screenshotSize.y - p.y;
    gl_Position = vec4(to_world(p - cameraPos * vec2(1.0, -1.0)), 0.0, 1.0);
    vUv = mirror ? vec2(1.0 - aUv.x, aUv.y) : aUv;
}
