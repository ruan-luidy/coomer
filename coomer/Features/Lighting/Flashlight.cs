using Coomer.Features.Configuration;
using System.Numerics;

namespace Coomer.Features.Lighting;

public sealed class Flashlight
{
  public const float InitialDeltaRadius = 150.0f;
  private const float DeltaRadiusDeceleration = 10.0f;

  public bool IsEnabled;
  public float Shadow;
  public float Radius = 200.0f;
  public float DeltaRadius;

  public float TargetRadius = 200.0f;
  public Vector2 Position;
  public Vector2 Velocity;
  public Vector2 Stretch;
  public float Squeeze;

  public void Update(Config config, float dt, Vector2 cursor)
  {
    if (MathF.Abs(DeltaRadius) > 1.0f)
    {
      TargetRadius = MathF.Max(50.0f, TargetRadius + DeltaRadius * dt);
      DeltaRadius -= DeltaRadius * DeltaRadiusDeceleration * dt;
    }

    if (IsEnabled && config.BubbleRigid)
    {
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
        Stretch += (velNorm * stretchAmount - Stretch) * k;
        Squeeze += (stretchAmount * config.BubbleSqueezeFactor - Squeeze) * k;
      }
      else
      {
        Stretch -= Stretch * k;
        Squeeze -= Squeeze * k;
      }
    }
    else
    {
      // gruda no cursor sem fisica pra nao "pular" ao reabilitar
      Position = cursor;
      Velocity = Vector2.Zero;
      Stretch = Vector2.Zero;
      Squeeze = 0f;
    }

    Radius += (TargetRadius - Radius) * MathF.Min(1.0f, 8.0f * dt);

    Shadow = IsEnabled
      ? MathF.Min(Shadow + 6.0f * dt, 0.55f)
      : MathF.Max(Shadow - 6.0f * dt, 0.0f);
  }
}
