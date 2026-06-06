using System.Numerics;
using System.Reflection;
using Silk.NET.OpenGL;

namespace Coomer.Features.Rendering;

/// <summary>
/// Porte de <c>newShader</c>/<c>newShaderProgram</c> de boomer.nim.
/// Compila + linka um programa GLSL e expoe helpers de uniform.
/// </summary>
public sealed class Shader : IDisposable
{
  private readonly GL _gl;
  public uint Handle { get; }

  public Shader(GL gl, string vertexSource, string fragmentSource, (uint location, string name)[] attribs)
  {
    _gl = gl;

    uint vertex = Compile(ShaderType.VertexShader, vertexSource);
    uint fragment = Compile(ShaderType.FragmentShader, fragmentSource);

    Handle = gl.CreateProgram();
    gl.AttachShader(Handle, vertex);
    gl.AttachShader(Handle, fragment);

    // GLSL 130 nao tem layout(location=...), entao fixamos os indices aqui (antes do link).
    foreach (var (location, name) in attribs)
      gl.BindAttribLocation(Handle, location, name);

    gl.LinkProgram(Handle);
    gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
    if (linked == 0)
      throw new Exception($"Falha ao linkar shader: {gl.GetProgramInfoLog(Handle)}");

    gl.DetachShader(Handle, vertex);
    gl.DetachShader(Handle, fragment);
    gl.DeleteShader(vertex);
    gl.DeleteShader(fragment);
  }

  private uint Compile(ShaderType type, string source)
  {
    uint shader = _gl.CreateShader(type);
    _gl.ShaderSource(shader, source);
    _gl.CompileShader(shader);
    _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
    if (ok == 0)
      throw new Exception($"Erro compilando {type}: {_gl.GetShaderInfoLog(shader)}");
    return shader;
  }

  public void Use() => _gl.UseProgram(Handle);
  public void SetInt(string name, int value) => _gl.Uniform1(_gl.GetUniformLocation(Handle, name), value);
  public void SetFloat(string name, float value) => _gl.Uniform1(_gl.GetUniformLocation(Handle, name), value);
  public void SetVec2(string name, Vector2 v) => _gl.Uniform2(_gl.GetUniformLocation(Handle, name), v.X, v.Y);

  public void Dispose() => _gl.DeleteProgram(Handle);
}

/// <summary>Le os shaders "bakeados" no exe como recurso embutido (sem dependencia de arquivo).</summary>
public static class EmbeddedShader
{
  public static string Load(string fileName)
  {
    var asm = Assembly.GetExecutingAssembly();
    var resourceName = $"Coomer.Shaders.{fileName}";
    using var stream = asm.GetManifestResourceStream(resourceName)
        ?? throw new FileNotFoundException($"Shader embutido nao encontrado: {resourceName}");
    using var reader = new StreamReader(stream);
    return reader.ReadToEnd();
  }
}
