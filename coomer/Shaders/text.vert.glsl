#version 130
in vec2 aPos;
in vec2 aUv;
out vec2 vUv;
uniform vec2 windowSize;

void main()
{
    gl_Position = vec4(aPos.x / windowSize.x * 2.0 - 1.0,
                       1.0 - aPos.y / windowSize.y * 2.0,
                       0.0, 1.0);
    vUv = aUv;
}
