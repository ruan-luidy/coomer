#version 130
in mediump vec2 vUv;
out mediump vec4 color;

uniform sampler2D tex;
uniform float opacity;
uniform vec4 tint;

uniform bool invertRect;
uniform vec2 invertMin;
uniform vec2 invertMax;
uniform vec2 fragWindowSize;

uniform bool flEnabled;
uniform float flShadow;
uniform float flRadius;
uniform vec2 bubblePos;
uniform vec2 bubbleStretch;
uniform float bubbleSqueeze;

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

void main()
{
    vec4 c = texture(tex, vUv);
    c.rgb *= tint.rgb;
    c.a *= opacity * tint.a;
    if (c.a < 0.01) discard;

    if (flShadow > 0.001)
    {
        vec2 frag = gl_FragCoord.xy;
        vec2 center = vec2(bubblePos.x, fragWindowSize.y - bubblePos.y);
        float sd = sdfBubble(frag, center, flRadius, bubbleStretch, bubbleSqueeze);
        float edgeAlpha = smoothstep(-2.0, 0.0, sd);
        c.rgb = mix(c.rgb, vec3(0.0), min(edgeAlpha, flShadow));
    }

    if (invertRect)
    {
        vec2 fs = vec2(gl_FragCoord.x, fragWindowSize.y - gl_FragCoord.y);
        if (fs.x >= invertMin.x && fs.x <= invertMax.x
            && fs.y >= invertMin.y && fs.y <= invertMax.y)
        {
            vec3 inv = 1.0 - c.rgb;
            float lum = dot(inv, vec3(0.299, 0.587, 0.114));
            float cap = 0.55;
            if (lum > cap) inv *= cap / lum;
            c.rgb = inv;
        }
    }
    color = c;
}
