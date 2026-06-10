using System.Numerics;

namespace Coomer.Features.Drawing;

public enum DrawShape
{
  Free,
  Line,
  Arrow,
  Rect,
  Circle,
}

// Pontos em coord de imagem (sem mirror). Thickness em pixel de imagem — escala com zoom.
public sealed class Stroke
{
  public DrawShape Shape;
  public List<Vector2> Points = new();
  public Vector4 Color;
  public float Thickness;
}
