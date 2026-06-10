using System.Numerics;
using Coomer.Features.Capture;
using Coomer.Features.Navigation;

namespace Coomer.Features.Lighting;

public sealed class ColorPicker
{
  public bool IsEnabled;
  public string LastHex = "";
  public Vector4 LastColor;
  public readonly ColorHistory History = new();

  public string PickAt(Vector2 cursor, Camera camera, Screenshot screenshot, Vector2 windowSize)
  {
    var centered = cursor - windowSize * 0.5f;
    var world = centered / camera.Scale;
    var shotPos = world + camera.Position + new Vector2(screenshot.Width, screenshot.Height) * 0.5f;

    int x = Math.Clamp((int)shotPos.X, 0, screenshot.Width - 1);
    int y = Math.Clamp((int)shotPos.Y, 0, screenshot.Height - 1);
    string hex = screenshot.GetPixelHex(x, y);

    int i = (y * screenshot.Width + x) * 4;
    byte b = screenshot.Pixels[i + 0];
    byte g = screenshot.Pixels[i + 1];
    byte r = screenshot.Pixels[i + 2];
    var color = new Vector4(r / 255f, g / 255f, b / 255f, 1f);

    LastHex = hex;
    LastColor = color;
    History.Push(color);
    return hex;
  }
}
