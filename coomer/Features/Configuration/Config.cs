using System.Globalization;

namespace Coomer.Features.Configuration;

/// <summary>
/// Porte de <c>config.nim</c>. Parametros de zoom/pan lidos de um arquivo
/// texto simples no formato <c>chave = valor</c> (linhas com <c>#</c> sao comentarios).
/// </summary>
public sealed class Config
{
  public float MinScale { get; set; } = 1.0f; // 1.0 = nao deixa encolher abaixo da tela cheia
  public float ScrollSpeed { get; set; } = 1.5f;
  public float DragFriction { get; set; } = 6.0f;
  public float ScaleFriction { get; set; } = 4.0f;

  public static Config Default() => new();

  public static Config Load(string path)
  {
    var config = Default();
    foreach (var raw in File.ReadLines(path))
    {
      var line = raw.Trim();
      if (line.Length == 0 || line[0] == '#')
        continue;

      var idx = line.IndexOf('=');
      if (idx < 0)
        continue;

      var key = line[..idx].Trim();
      var value = line[(idx + 1)..].Trim();
      var number = float.Parse(value, CultureInfo.InvariantCulture);

      switch (key)
      {
        case "min_scale": config.MinScale = number; break;
        case "scroll_speed": config.ScrollSpeed = number; break;
        case "drag_friction": config.DragFriction = number; break;
        case "scale_friction": config.ScaleFriction = number; break;
        default: throw new InvalidDataException($"Chave de config desconhecida `{key}`");
      }
    }
    return config;
  }

  /// <summary>Recarrega os valores deste objeto a partir do arquivo (usado na tecla R).</summary>
  public void Reload(string path)
  {
    var fresh = Load(path);
    MinScale = fresh.MinScale;
    ScrollSpeed = fresh.ScrollSpeed;
    DragFriction = fresh.DragFriction;
    ScaleFriction = fresh.ScaleFriction;
  }

  public void Save(string path)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    var c = CultureInfo.InvariantCulture;
    using var w = new StreamWriter(path);
    w.WriteLine($"min_scale = {MinScale.ToString(c)}");
    w.WriteLine($"scroll_speed = {ScrollSpeed.ToString(c)}");
    w.WriteLine($"drag_friction = {DragFriction.ToString(c)}");
    w.WriteLine($"scale_friction = {DragFriction.ToString(c)}");
  }
}