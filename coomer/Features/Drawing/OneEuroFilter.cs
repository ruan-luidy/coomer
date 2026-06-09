using System.Numerics;

namespace Coomer.Features.Drawing;

/// <summary>
/// "1€ Filter" (Casiez et al., CHI 2012): low-pass adaptativo — quando o
/// cursor anda devaga, corta tremedeira agressivamente; quando anda rapido,
/// cede passagem pra nao introduzir lag. E o filtro padrao da industria pra
/// suavizar inpu de mouse/stylus em desenho.
/// 
/// Aplica componente a componente em X/Y. Reset() limpa o estado entre tracos.
/// </summary>
public sealed class OneEuroFilterV2
{
  /// <summary>Cutoff base em Hz (menor = mais smooth quando parado). 1.0 mata tremos de mao bem.</summary>
  public float MinCutoff = 1.0f;
  /// <summary>Quanto a velocidade afrouxa o filtro (maior = menos lag em movimento rapido).</summary>
  public float Beta = 0.01f;
  /// <summary>Cutoff do estimador de derivada — fixo, basicamente sempre 1Hz.</summary>
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
    float aD = Alpha(DCutoff, dt);
    var dxHat = _dx + aD * (dx - _dx);
    float speed = dxHat.Length();
    float cutoff = MinCutoff + Beta * speed;
    float a = Alpha(cutoff, dt);
    var xHat = _x + a * (x - _x);

    _x = xHat;
    _dx = dxHat;
    return xHat;
  }

  // a = rc / (rc + 1), rc = 2*pi*cutoff*dt. Forma fechada do EMA equivalente.
  private static float Alpha(float cutoff, float dt)
  {
    float r = 2f * MathF.PI * cutoff * dt;
    return r / (r + 1f);
  }
}