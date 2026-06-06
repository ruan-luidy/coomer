namespace Coomer.Features.Lighting;

/// <summary>
/// Porte do <c>Flashlight</c> de boomer.nim. Efeito de "lanterna": escurece a tela
/// inteira menos um circulo ao redor do cursor. O raio tambem tem inercia.
/// </summary>
public sealed class Flashlight
{
  public const float InitialDeltaRadius = 150.0f;
  private const float DeltaRadiusDeceleration = 10.0f;

  public bool IsEnabled;
  public float Shadow;
  public float Radius = 120.0f;
  public float DeltaRadius;

  public void Update(float dt)
  {
    if (MathF.Abs(DeltaRadius) > 1.0f)
    {
      Radius = MathF.Max(0.0f, Radius + DeltaRadius * dt);
      DeltaRadius -= DeltaRadius * DeltaRadiusDeceleration * dt;
    }

    Shadow = IsEnabled
      ? MathF.Min(Shadow + 6.0f * dt, 0.8f)
      : MathF.Max(Shadow - 6.0f * dt, 0.0f);
  }
}