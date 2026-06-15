# Third-party licenses

## ghostty-blackhole

`coomer/Shaders/blackhole.glsl` is adapted from
[s0xDk/ghostty-blackhole](https://github.com/s0xDk/ghostty-blackhole) (the
Ghostty terminal pomodoro/token-mode black hole shader). The shader is itself
an independent screen-space approximation inspired by Eric Bruneton's
[black hole shader](https://github.com/ebruneton/black_hole_shader) (BSD-3,
not used directly). Only the GLSL fragment was adapted; the terminal-specific
modes (pomodoro, token, cursor-color decode) and the macOS tuner app were not
imported.

```
MIT License

Copyright (c) 2026 s13k <s13k@pm.me>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
