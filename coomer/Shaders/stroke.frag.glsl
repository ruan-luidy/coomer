#version 130
in mediump vec2 vPosImg;
in mediump vec2 vA;
in mediump vec2 vB;
in mediump float vHalf;
out mediump vec4 color;
uniform vec4 uColor;

uniform bool invertRect;
uniform vec2 invertMin;
uniform vec2 invertMax;
uniform vec2 fragWindowSize;
uniform int invertMode; // 0 = negativo, 1 = glass (passthrough nos strokes)

float distSeg(vec2 p, vec2 a, vec2 b) {
    vec2 ba = b - a;
    float l2 = dot(ba, ba);
    if (l2 < 1e-6) return length(p - a);
    float t = clamp(dot(p - a, ba) / l2, 0.0, 1.0);
    return length((p - a) - ba * t);
}

void main()
{
    float d = distSeg(vPosImg, vA, vB);
    float aa = max(fwidth(d), 0.0001);
    float alpha = 1.0 - smoothstep(vHalf - aa, vHalf + aa, d);
    if (alpha <= 0.001) discard;

    vec3 rgb = uColor.rgb;
    if (invertRect)
    {
        vec2 fs = vec2(gl_FragCoord.x, fragWindowSize.y - gl_FragCoord.y);
        bool inside = fs.x >= invertMin.x && fs.x <= invertMax.x
                   && fs.y >= invertMin.y && fs.y <= invertMax.y;
        if (invertMode == 0 && inside) rgb = 1.0 - rgb;
        else if (invertMode == 1 && !inside) rgb *= 0.45;
    }
    color = vec4(rgb, uColor.a * alpha);
}
