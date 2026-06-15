namespace Coomer.Features.Effects;

// Estado do efeito black hole (tecla G). O shader nao guarda estado entre
// frames, entao a animacao mora aqui: Time avanca sempre (deriva/disco) e
// Intensity faz easing exponencial em direcao a 0/1 — cresce ao ligar, encolhe
// ao desligar. O Renderer so desenha o shader enquanto Visible.
public sealed class BlackHole
{
  public bool Active { get; private set; }
  public float Time { get; private set; }      // segundos, sempre crescendo
  public float Intensity { get; private set; } // eased 0..1

  // ainda vale desenhar enquanto a sombra termina de encolher
  public bool Visible => Active || Intensity > 0.01f;

  public void Toggle() => Active = !Active;

  public void Tick(float dt)
  {
    Time += dt;
    float target = Active ? 1f : 0f;
    // sobe mais lento (cresce dramatico), cai mais rapido (some limpo)
    float rate = Active ? 3.5f : 7f;
    Intensity += (target - Intensity) * MathF.Min(1f, rate * dt);
    if (!Active && Intensity < 0.001f) Intensity = 0f;
  }
}
