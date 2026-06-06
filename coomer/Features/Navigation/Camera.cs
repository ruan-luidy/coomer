using System.Numerics;
using Coomer.Features.Configuration;

namespace Coomer.Features.Navigation;

/// <summary>Estado do mouse usado pela navegacao (porte do <c>Mouse</c> de navigation.nim).</summary>
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

  private readonly Vector2 _imageSize;

  public Camera(Vector2 imageSize) => _imageSize = imageSize;

  /// <summary>Converte um ponto de tela para coordenadas de "mundo" (dividindo pela escala).</summary>
  public Vector2 World(Vector2 v) => v / Scale;

  public void Update(Config config, float dt, bool dragging, Vector2 windowSize)
  {
    if (MathF.Abs(DeltaScale) > 0.5f)
    {
      var p0 = (ScalePivot - windowSize * 0.5f) / Scale;
      Scale = MathF.Max(Scale + DeltaScale * dt, config.MinScale);
      var p1 = (ScalePivot - windowSize * 0.5f) / Scale;
      Position += p0 - p1;

      DeltaScale -= DeltaScale * dt * config.ScaleFriction;
    }

    if (!dragging && Velocity.Length() > VelocityThreshold)
    {
      Position += Velocity * dt;
      Velocity -= Velocity * dt * config.DragFriction;
    }

    ClampToImage(windowSize);
  }

  /// <summary>
  /// Impede dar pan pra fora da imagem: limita a posicao para que a regiao visivel
  /// (windowSize/Scale em pixels da imagem) nunca passe das bordas da screenshot.
  /// </summary>
  private void ClampToImage(Vector2 windowSize)
  {
    var maxX = (_imageSize.X - windowSize.X / Scale) * 0.5f;
    var maxY = (_imageSize.Y - windowSize.Y / Scale) * 0.5f;
    Position.X = maxX > 0f ? Math.Clamp(Position.X, -maxX, maxX) : 0f;
    Position.Y = maxY > 0f ? Math.Clamp(Position.Y, -maxY, maxY) : 0f;
  }

  /// <summary>Volta camera ao estado inicial (tecla 0).</summary>
  public void Reset()
  {
    Scale = 1.0f;
    DeltaScale = 0.0f;
    Position = Vector2.Zero;
    Velocity = Vector2.Zero;
  }
}