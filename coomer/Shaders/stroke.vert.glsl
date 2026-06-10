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
// True = aPos/aSegA/aSegB vem em pixels de TELA (UI overlay: ringue, swatches,
// rect de region copy, etc). Pula o transform de camera/imagem e mapeia direto
// pro clip. False = pixel de imagem (comportamento padrao dos tracos).
uniform bool uScreenSpace;

vec2 to_world(vec2 v) {
    vec2 ratio = vec2(
        windowSize.x / screenshotSize.x / cameraScale,
        windowSize.y / screenshotSize.y / cameraScale);
    return vec2((v.x / screenshotSize.x * 2.0 - 1.0) / ratio.x,
                (v.y / screenshotSize.y * 2.0 - 1.0) / ratio.y);
}

void main()
{
    if (uScreenSpace) {
        // Mapeia screen pixel (Y-down) pra clip space (Y-up).
        gl_Position = vec4(aPos.x / windowSize.x * 2.0 - 1.0,
                           1.0 - aPos.y / windowSize.y * 2.0,
                           0.0, 1.0);
    } else {
        vec2 p = aPos;
        if (mirror) p.x = screenshotSize.x - p.x;
        p.y = screenshotSize.y - p.y;
        gl_Position = vec4(to_world(p - cameraPos * vec2(1.0, -1.0)), 0.0, 1.0);
    }

    // Pro AA do frag, vPosImg/vA/vB vivem no MESMO espaco do aPos. Em screen
    // mode sao screen pixels; em image mode sao image pixels. fwidth se vira
    // em ambos.
    vPosImg = aPos;
    vA = aSegA;
    vB = aSegB;
    vHalf = aHalfWidth;
}
