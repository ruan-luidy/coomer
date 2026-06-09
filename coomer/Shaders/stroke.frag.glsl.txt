#version 130
in mediump vec2 vPosImg;
in mediump vec2 vA;
in mediump vec2 vB;
in mediump float vHalf;
out mediump vec4 color;
uniform vec4 uColor;

// Distancia de p ao segmento AB. h=0 quando A==B (vira distancia ate ponto).
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
    // fwidth(d) = quantos units-de-imagem por pixel de tela na vizinhanca do
    // fragmento. Usar isso pro smoothstep da uma banda AA de ~2 pixels em
    // qualquer zoom, sem precisar saber cameraScale aqui.
    float aa = max(fwidth(d), 0.0001);
    float alpha = 1.0 - smoothstep(vHalf - aa, vHalf + aa, d);
    if (alpha <= 0.001) discard;
    color = vec4(uColor.rgb, uColor.a * alpha);
}
