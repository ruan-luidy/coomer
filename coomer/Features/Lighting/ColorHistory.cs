using System.Numerics;

namespace Coomer.Features.Lighting;

public sealed class ColorHistory
{
  public const int Capacity = 8;

  public readonly LinkedList<Vector4> Entries = new();

  public void Push(Vector4 color)
  {
    var node = Entries.First;
    while (node != null)
    {
      if (Same(node.Value, color))
      {
        Entries.Remove(node);
        Entries.AddFirst(node);
        return;
      }
      node = node.Next;
    }

    Entries.AddFirst(color);
    while (Entries.Count > Capacity)
      Entries.RemoveLast();
  }

  public void Clear() => Entries.Clear();

  private static bool Same(Vector4 a, Vector4 b)
  {
    const float eps = 0.5f / 255f;
    return MathF.Abs(a.X - b.X) < eps
        && MathF.Abs(a.Y - b.Y) < eps
        && MathF.Abs(a.Z - b.Z) < eps
        && MathF.Abs(a.W - b.W) < eps;
  }
}
