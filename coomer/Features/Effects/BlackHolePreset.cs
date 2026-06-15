namespace Coomer.Features.Effects;

// Cada preset e uma tupla das tunables que viraram uniform: inclinacao do disco,
// temperatura do anel quente (Kelvin), mix doppler/beaming, ganho de emissao,
// velocidade da deriva pela tela e taxa minima da dilatacao temporal.
public readonly record struct BlackHolePreset(
    string Name,
    float DiskIncl,
    float DiskTemp,
    float DopplerMix,
    float DiskGain,
    float DiskBeam,
    float DriftSpeed,
    float DilationMin);

public static class BlackHolePresets
{
  public static readonly BlackHolePreset[] All =
  {
    new("DEFAULT",   1.50f,  5500f, 0.60f, 2.2f, 2.5f, 1.0f, 0.20f),
    // gargantua (Interstellar): face-on, disco quente brilhante, dilatacao forte
    new("GARGANTUA", 0.30f,  6500f, 0.40f, 2.6f, 2.0f, 0.5f, 0.10f),
    // quasar: edge-on, azul violento, beaming alto
    new("QUASAR",    1.40f, 12000f, 0.90f, 3.0f, 3.5f, 1.2f, 0.15f),
    // M87*: quase face-on, vermelho fraco, calmo (foto da EHT)
    new("M87",       0.20f,  3500f, 0.50f, 1.6f, 1.8f, 0.3f, 0.30f),
    // inferno: tudo no talo, deriva rapida
    new("INFERNO",   1.20f,  9000f, 0.85f, 3.5f, 3.2f, 2.0f, 0.05f),
    // zen: sutil, disco fraco, mal se mexe
    new("ZEN",       1.05f,  4200f, 0.30f, 1.2f, 1.5f, 0.2f, 0.40f),
  };
}
