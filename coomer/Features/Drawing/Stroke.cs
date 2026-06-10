using System.Numerics;

namespace Coomer.Features.Drawing;

/// <summary>Tipo de traco: mao livre, segmento reto, seta, retangulo (contorno) ou circulo.</summary>
public enum DrawShape
{
  Free,
  Line,
  Arrow,
  Rect,
  Circle,
}

/// <summary>
/// Um traco do usuario. Pontos ficam em coords de imagem (pixel da screenshot)
/// � assim o traco gruda no conteudo: zoom/pan/mirro mexem com a imagem e o
/// desenho junto.
/// </summary>
public sealed class Stroke
{
  public DrawShape Shape;
  public List<Vector2> Points = new();
  public Vector4 Color;
  /// <summary>Espessura em pixels de imagem (escala com o zoom � feeling de "tinta na pagina").</summary>
  public float Thickness;
}