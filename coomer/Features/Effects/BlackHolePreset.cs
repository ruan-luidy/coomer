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
    // padrao: inclinacao media, disco quente equilibrado, dilatacao moderada
    new("DEFAULT",   1.50f,  5500f, 0.60f, 2.2f, 2.5f, 1.0f, 0.20f),
    // blazar: quase edge-on, azul violento com beaming relativistico no talo
    // (analogo terrestre: jato apontado quase pra camera)
    new("BLAZAR",    1.45f, 15000f, 0.95f, 3.2f, 4.0f, 1.5f, 0.10f),
    // pure lens: disco desligado (gain 0). So a sombra, o anel de fotons e a
    // lente gravitacional do screenshot dobrando ao redor. Outros params do
    // disco viram ruido — ficam em valores neutros.
    new("PURE LENS", 1.20f,  5500f, 0.00f, 0.0f, 1.0f, 0.5f, 0.30f),
  };
}
