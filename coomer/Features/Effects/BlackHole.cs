namespace Coomer.Features.Effects;

// Estado do efeito black hole (tecla G).
//
// Modo: G cicla off -> interactive -> passthrough -> off.
//   interactive: input vai pro overlay; ctrl+scroll resize, , cicla preset,
//                [/] inclinacao. O buraco fica na sua Lissajous interna.
//   passthrough: window vira click-through (WS_EX_TRANSPARENT); voce mexe
//                no PC enquanto o buraco fica desenhando por cima. Esc (via
//                hook global de teclado, instalado pelo CoomerApp) volta pro
//                interactive.
//
// Animacao: Time avanca sempre (deriva/disco). Intensity faz easing
// exponencial em direcao a 0/1 conforme Active liga/desliga. HoleScale e
// ajustado por ctrl+scroll.
public sealed class BlackHole
{
  public BlackHoleMode Mode { get; private set; } = BlackHoleMode.Off;
  public float Time { get; private set; }
  public float Intensity { get; private set; }

  public bool Active => Mode != BlackHoleMode.Off;
  public bool Visible => Active || Intensity > 0.01f;
  public bool Passthrough => Mode == BlackHoleMode.Passthrough;
  public bool Interactive => Mode == BlackHoleMode.Interactive;

  // notifica o CoomerApp quando o modo muda (pra trocar click-through e hook)
  public event Action<BlackHoleMode>? ModeChanged;

  // Preset atual (tabela em BlackHolePreset.cs)
  public int PresetIndex { get; private set; }
  public BlackHolePreset Preset => BlackHolePresets.All[PresetIndex];

  // Multiplicador sobre HOLE_RADIUS (1.0 = default); ctrl+scroll mexe nele
  public float HoleScale { get; private set; } = 1f;

  // Override de inclinacao do disco — NaN = usa o do preset; [/]/]/ mexem
  public float DiskInclOverride { get; private set; } = float.NaN;
  public float EffectiveDiskIncl =>
      float.IsNaN(DiskInclOverride) ? Preset.DiskIncl : DiskInclOverride;

  public void Cycle()
  {
    var next = Mode switch
    {
      BlackHoleMode.Off => BlackHoleMode.Interactive,
      BlackHoleMode.Interactive => BlackHoleMode.Passthrough,
      _ => BlackHoleMode.Off,
    };
    SetMode(next);
  }

  public void ForceOff() => SetMode(BlackHoleMode.Off);
  public void ExitPassthrough()
  {
    if (Mode == BlackHoleMode.Passthrough) SetMode(BlackHoleMode.Interactive);
  }

  private void SetMode(BlackHoleMode mode)
  {
    if (Mode == mode) return;
    Mode = mode;
    ModeChanged?.Invoke(mode);
  }

  public void CyclePreset(int dir)
  {
    int n = BlackHolePresets.All.Length;
    PresetIndex = ((PresetIndex + dir) % n + n) % n;
    // novo preset reseta override de inclinacao (pra ver o look do preset cru)
    DiskInclOverride = float.NaN;
  }

  public void ScaleDelta(float d) => HoleScale = Math.Clamp(HoleScale + d, 0.25f, 3.5f);

  public void DiskInclDelta(float d)
  {
    float current = EffectiveDiskIncl;
    DiskInclOverride = Math.Clamp(current + d, 0f, MathF.PI / 2f);
  }

  public void Tick(float dt)
  {
    Time += dt;
    float target = Active ? 1f : 0f;
    float rate = Active ? 3.5f : 7f;
    Intensity += (target - Intensity) * MathF.Min(1f, rate * dt);
    if (!Active && Intensity < 0.001f) Intensity = 0f;
  }

  // Centro do buraco em uv [0..1] de tela — mesmo Lissajous do shader.
  // Usado pela fisica de mouse no passthrough pra saber onde puxar o cursor.
  public System.Numerics.Vector2 ComputeHoleCenterUv()
  {
    float t = Time * Preset.DriftSpeed * 0.25f;
    float x = 0.5f + 0.16f * (0.75f * MathF.Sin(t * 0.37f) + 0.25f * MathF.Sin(t * 0.83f + 1f));
    float y = 0.5f + 0.18f * (0.70f * MathF.Sin(t * 0.54f + 2.1f) + 0.30f * MathF.Sin(t * 1.07f));
    return new System.Numerics.Vector2(x, y);
  }
}

public enum BlackHoleMode
{
  Off,
  Interactive,
  Passthrough,
}
