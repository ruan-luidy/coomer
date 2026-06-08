using System.Numerics;
using Coomer.Features.Capture;
using Coomer.Features.Navigation;

namespace Coomer.Features.Lighting;

public sealed class ColorPicker
{
  public bool IsEnabled;
  public string LastHex = "";

  /// <summary>
  /// Converte <paramref name="cursor"/> (coord da janela) pra coord da
  /// screenshot e devolve o pixel em "#RRGGBB". A camera tem origem no centro,
  /// entao descontamos windowSize/2 antes de dividir pela escala.
  /// </summary>
  public string PickAt(Vector2 cursor, Camera camera, Screenshot screenshot, Vector2 windowSize)
  {
    var centered = cursor - windowSize * 0.5f;
    var world = centered / camera.Scale;
    var shotPos = world + camera.Position + new Vector2(screenshot.Width, screenshot.Height) * 0.5f;

    int x = Math.Clamp((int)shotPos.X, 0, screenshot.Width - 1);
    int y = Math.Clamp((int)shotPos.Y, 0, screenshot.Height - 1);
    return screenshot.GetPixelHex(x, y);
  }
}
