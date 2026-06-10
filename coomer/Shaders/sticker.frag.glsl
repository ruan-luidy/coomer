#version 130
in mediump vec2 vUv;
out mediump vec4 color;

uniform sampler2D tex;
uniform float opacity;

uniform bool invertRect;
uniform vec2 invertMin;
uniform vec2 invertMax;
uniform vec2 fragWindowSize;

void main()
{
    vec4 c = texture(tex, vUv);
    c.a *= opacity;
    if (c.a < 0.01) discard;

    if (invertRect)
    {
        vec2 fs = vec2(gl_FragCoord.x, fragWindowSize.y - gl_FragCoord.y);
        if (fs.x >= invertMin.x && fs.x <= invertMax.x
            && fs.y >= invertMin.y && fs.y <= invertMax.y)
            c.rgb = 1.0 - c.rgb;
    }
    color = c;
}
