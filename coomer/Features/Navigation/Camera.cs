using System.Numerics;
using Coomer.Features.Configuration;

namespace Coomer.Features.Navigation;

public struct Mouse
{
  public Vector2 Current;
  public Vector2 Previous;
  public bool Drag;
}

public sealed class Camera
{
  private const float VelocityThreshold = 15.0f;

  public Vector2 Position;
  public Vector2 Velocity;
  public float Scale = 1.0f;
  public float DeltaScale;
  public Vector2 ScalePivot;

  public Vector2 TargetPosition;
  public float TargetScale = 1.0f;
  public bool LerpingToTarget;

  private readonly Vector2 _imageSize;

  public Camera(Vector2 imageSize) => _imageSize = imageSize;

  public Vector2 World(Vector2 v) => v / Scale;

  public void Update(Config config, float dt, bool dragging, Vector2 windowSize)
  {
    // Antes do bloco de zoom: um delta de scale acidental nao deve quebrar o lerp.
    if (LerpingToTarget)
    {
      float t = MathF.Min(1.0f, dt * config.CameraRecenterLerpSpeed);
      Position = Vector2.Lerp(Position, TargetPosition, t);
      Scale = Scale + (TargetScale - Scale) * t;

      if ((Position - TargetPosition).Length() < 0.5f
          && MathF.Abs(TargetScale - Scale) < 0.001f)
      {
        Position = TargetPosition;
        Scale = TargetScale;
        Velocity = Vector2.Zero;
        DeltaScale = 0f;
        LerpingToTarget = false;
      }
    }

    if (MathF.Abs(DeltaScale) > 0.5f)
    {
      var p0 = (ScalePivot - windowSize * 0.5f) / Scale;
      Scale = MathF.Max(Scale + DeltaScale * dt, config.MinScale);
      var p1 = (ScalePivot - windowSize * 0.5f) / Scale;
      Position += p0 - p1;

      DeltaScale -= DeltaScale * dt * config.ScaleFriction;
    }

    if (config.PanInertia && !dragging && Velocity.Length() > VelocityThreshold)
    {
      Position += Velocity * dt;
      Velocity -= Velocity * dt * config.DragFriction;
    }

    ClampToImage(windowSize);
  }

  private void ClampToImage(Vector2 windowSize)
  {
    var maxX = (_imageSize.X - windowSize.X / Scale) * 0.5f;
    var maxY = (_imageSize.Y - windowSize.Y / Scale) * 0.5f;
    Position.X = maxX > 0f ? Math.Clamp(Position.X, -maxX, maxX) : 0f;
    Position.Y = maxY > 0f ? Math.Clamp(Position.Y, -maxY, maxY) : 0f;
  }

  public void Reset(bool animate)
  {
    if (animate)
    {
      TargetPosition = Vector2.Zero;
      TargetScale = 1.0f;
      LerpingToTarget = true;
      DeltaScale = 0f;
      Velocity = Vector2.Zero;
    }
    else
    {
      Scale = 1.0f;
      DeltaScale = 0.0f;
      Position = Vector2.Zero;
      Velocity = Vector2.Zero;
      LerpingToTarget = false;
    }
  }

  public void Pan(Vector2 delta)
  {
    LerpingToTarget = false;
    Position += delta;
    Velocity = Vector2.Zero;
  }
}
