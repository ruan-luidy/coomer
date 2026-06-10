#version 130
in mediump vec2 vUv;
out mediump vec4 color;

uniform sampler2D tex;
uniform vec4 uColor;

void main()
{
    vec4 t = texture(tex, vUv);
    // Modula RGB: texto (textura branca + alpha variavel) tinge por uColor;
    // sticker thumbnail (RGBA real) sai com cor propria quando uColor = (1,1,1,a).
    color = vec4(t.rgb * uColor.rgb, t.a * uColor.a);
}
