using Coomer.Features.Configuration;
using System.Numerics;

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
  public float Radius = 200.0f;
  public float DeltaRadius;

  public float TargetRadius = 200.0f;     // raio interpola ate aqui
  public Vector2 Position;                // posicao atual (com inercia)
  public Vector2 Velocity;
  public Vector2 Stretch;                 // direcao do "ovo" (cresce com velocidade)
  public float Squeeze;                   // espremida perpendicular

  public void Update(Config config, float dt, Vector2 cursor)
  {
    if (MathF.Abs(DeltaRadius) > 1.0f)
    {
      TargetRadius = MathF.Max(50.0f, TargetRadius + DeltaRadius * dt);
      DeltaRadius -= DeltaRadius * DeltaRadiusDeceleration * dt;
    }

    if (IsEnabled && config.BubbleRigid)
    {
      // Modo rigido: bolha cola no cursor sem mola/amortecimento. Mantem so a
      // animacao do raio e da sombra; deformacao zerada.
      Position = cursor;
      Velocity = Vector2.Zero;
      Stretch = Vector2.Zero;
      Squeeze = 0f;
    }
    else if (IsEnabled)
    {
      var displacement = Position - cursor;
      var spring = displacement * -config.BubbleSpringK;
      var damping = Velocity * -config.BubbleDamping;
      var accel = (spring + damping) / MathF.Max(0.001f, config.BubbleMass);

      Velocity += accel * dt;
      Position += Velocity * dt;

      float velMag = Velocity.Length();
      float k = config.BubbleDeformSmoothing * dt;
      if (velMag > 0.1f)
      {
        var velNorm = Velocity / velMag;
        float stretchAmount = velMag * config.BubbleStretchFactor;
        var targetStretch = velNorm * stretchAmount;
        Stretch += (targetStretch - Stretch) * k;
        Squeeze += (stretchAmount * config.BubbleSqueezeFactor - Squeeze) * k;
      }
      else
      {
        // recupera forma circular quando para
        Stretch -= Stretch * k;
        Squeeze -= Squeeze * k;
      }
    }
    else
    {
      // desligada: gruda no cursor sem fisica (assim ao reabilitar nao tem "pulo")
      Position = cursor;
      Velocity = Vector2.Zero;
      Stretch = Vector2.Zero;
      Squeeze = 0f;
    }

    // raio anima ate o alvo
    Radius += (TargetRadius - Radius) * MathF.Min(1.0f, 8.0f * dt);

    // Sombra cresce/diminui — pico em 0.55 deixa o blur do anel visivel
    // (a 0.8 sobrava so 20% da imagem e ficava quase preto).
    Shadow = IsEnabled
      ? MathF.Min(Shadow + 6.0f * dt, 0.55f)
      : MathF.Max(Shadow - 6.0f * dt, 0.0f);
  }
}