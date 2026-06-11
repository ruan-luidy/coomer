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
        if (fs.x >= invertMin.x && fs.x <= invertMax.x
            && fs.y >= invertMin.y && fs.y <= invertMax.y)
        {
            vec3 inv = 1.0 - rgb;
            float lum = dot(inv, vec3(0.299, 0.587, 0.114));
            float cap = 0.55;
            if (lum > cap) inv *= cap / lum;
            rgb = inv;
        }
    }
    color = vec4(rgb, uColor.a * alpha);
}
