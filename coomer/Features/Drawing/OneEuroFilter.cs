using System.Numerics;

namespace Coomer.Features.Drawing;

// 1€ Filter (Casiez et al., CHI 2012). Low-pass adaptativo: cutoff sobe com a
// velocidade pra cortar tremor parado sem introduzir lag em movimento rapido.
public sealed class OneEuroFilterV2
{
  public float MinCutoff = 1.0f;
  public float Beta = 0.01f;
  public float DCutoff = 1.0f;

  private Vector2 _x;
  private Vector2 _dx;
  private bool _init;

  public void Reset() => _init = false;

  public Vector2 Step(Vector2 x, float dt)
  {
    if (!_init || dt <= 1e-5f)
    {
      _x = x;
      _dx = Vector2.Zero;
      _init = true;
      return x;
    }

    var dx = (x - _x) / dt;
    var dxHat = _dx + Alpha(DCutoff, dt) * (dx - _dx);
    float cutoff = MinCutoff + Beta * dxHat.Length();
    var xHat = _x + Alpha(cutoff, dt) * (x - _x);

    _x = xHat;
    _dx = dxHat;
    return xHat;
  }

  private static float Alpha(float cutoff, float dt)
  {
    float r = MathF.Tau * cutoff * dt;
    return r / (r + 1f);
  }
}
