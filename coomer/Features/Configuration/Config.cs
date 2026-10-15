using System.Globalization;
using System.Text;

namespace Coomer.Features.Configuration;

// Config em sintaxe Lua-ish em %APPDATA%/coomer/config.lua
//   -- comentario de linha
//   --[[ comentario em bloco ]]
//   key = value         (number)
//   key = true|false    (bool)
//   key = "string"      (string)
// Sem tabelas/funcoes/expressoes, so atribuicao direta.
public sealed class Config
{
  public float MinScale { get; set; } = 1.0f;
  public float ScrollSpeed { get; set; } = 1.5f;
  public float DragFriction { get; set; } = 6.0f;
  public float ScaleFriction { get; set; } = 4.0f;

  public float CameraPanAmount { get; set; } = 200.0f;
  public bool LerpCameraRecenter { get; set; } = true;
  public float CameraRecenterLerpSpeed { get; set; } = 6.0f;
  public bool HideCursorOnFlashlight { get; set; } = true;
  public bool PanInertia { get; set; } = true;

  public float BubbleMass { get; set; } = 1.0f;
  public float BubbleSpringK { get; set; } = 80.0f;
  public float BubbleDamping { get; set; } = 8.0f;
  public float BubbleStretchFactor { get; set; } = 0.0001f;
  public float BubbleSqueezeFactor { get; set; } = 0.5f;
  public float BubbleDeformSmoothing { get; set; } = 8.0f;
  public bool BubbleRigid { get; set; } = false;

  public bool BlurBackground { get; set; } = false;
  public float BackgroundBlurRadius { get; set; } = 6.0f;
  public bool BlurOutsideFlashlight { get; set; } = false;
  public float OutsideFlashlightBlurRadius { get; set; } = 6.0f;

  public bool FlashlightFisheye { get; set; } = false;
  public float FisheyeStrength { get; set; } = 0.5f;
  public bool FlashlightClearGlass { get; set; } = false;
  public float ClearGlassZoom { get; set; } = 1.10f;

  // "negative" ou "glass" — controla o efeito dentro do rect de print (tecla B)
  public string PrintEffect { get; set; } = "negative";

  public static Config Default() => new();

  public static Config Load(string path)
  {
    var config = Default();
    var raw = File.ReadAllText(path);
    var clean = StripBlockComments(raw);

    foreach (var rawLine in clean.Split('\n'))
    {
      var line = StripLineComment(rawLine).Trim();
      if (line.Length == 0)
        continue;

      var idx = line.IndexOf('=');
      if (idx < 0)
        continue;

      var key = line[..idx].Trim();
      var value = line[(idx + 1)..].Trim();

      // linha ruim (chave desconhecida, numero invalido) so e ignorada e vai
      // pro log — antes uma chave errada jogava o config inteiro fora e o app
      // voltava pro default sem avisar.
      try
      {
        if (!Apply(config, key, value))
          Log.Error($"config: chave desconhecida `{key}` ignorada ({path})");
      }
      catch (FormatException)
      {
        Log.Error($"config: valor invalido `{value}` pra `{key}` ignorado ({path})");
      }
    }
    return config;
  }

  private static bool Apply(Config config, string key, string value)
  {
    switch (key)
    {
      case "min_scale": config.MinScale = ParseFloat(value); break;
      case "scroll_speed": config.ScrollSpeed = ParseFloat(value); break;
      case "drag_friction": config.DragFriction = ParseFloat(value); break;
      case "scale_friction": config.ScaleFriction = ParseFloat(value); break;

      case "camera_pan_amount": config.CameraPanAmount = ParseFloat(value); break;
      case "lerp_camera_recenter": config.LerpCameraRecenter = ParseBool(value); break;
      case "camera_recenter_lerp_speed": config.CameraRecenterLerpSpeed = ParseFloat(value); break;
      case "hide_cursor_on_flashlight": config.HideCursorOnFlashlight = ParseBool(value); break;
      case "pan_inertia": config.PanInertia = ParseBool(value); break;

      case "bubble_mass": config.BubbleMass = ParseFloat(value); break;
      case "bubble_spring_k": config.BubbleSpringK = ParseFloat(value); break;
      case "bubble_damping": config.BubbleDamping = ParseFloat(value); break;
      case "bubble_stretch_factor": config.BubbleStretchFactor = ParseFloat(value); break;
      case "bubble_squeeze_factor": config.BubbleSqueezeFactor = ParseFloat(value); break;
      case "bubble_deform_smoothing": config.BubbleDeformSmoothing = ParseFloat(value); break;
      case "bubble_rigid": config.BubbleRigid = ParseBool(value); break;

      case "blur_background": config.BlurBackground = ParseBool(value); break;
      case "background_blur_radius": config.BackgroundBlurRadius = ParseFloat(value); break;
      case "blur_outside_flashlight": config.BlurOutsideFlashlight = ParseBool(value); break;
      case "outside_flashlight_blur_radius": config.OutsideFlashlightBlurRadius = ParseFloat(value); break;

      case "flashlight_fisheye": config.FlashlightFisheye = ParseBool(value); break;
      case "fisheye_strength": config.FisheyeStrength = ParseFloat(value); break;
      case "flashlight_clear_glass": config.FlashlightClearGlass = ParseBool(value); break;
      case "clear_glass_zoom": config.ClearGlassZoom = ParseFloat(value); break;

      case "print_effect": config.PrintEffect = ParseString(value); break;

      default: return false;
    }
    return true;
  }

  public void Reload(string path)
  {
    var fresh = Load(path);
    MinScale = fresh.MinScale;
    ScrollSpeed = fresh.ScrollSpeed;
    DragFriction = fresh.DragFriction;
    ScaleFriction = fresh.ScaleFriction;

    CameraPanAmount = fresh.CameraPanAmount;
    LerpCameraRecenter = fresh.LerpCameraRecenter;
    CameraRecenterLerpSpeed = fresh.CameraRecenterLerpSpeed;
    HideCursorOnFlashlight = fresh.HideCursorOnFlashlight;
    PanInertia = fresh.PanInertia;

    BubbleMass = fresh.BubbleMass;
    BubbleSpringK = fresh.BubbleSpringK;
    BubbleDamping = fresh.BubbleDamping;
    BubbleStretchFactor = fresh.BubbleStretchFactor;
    BubbleSqueezeFactor = fresh.BubbleSqueezeFactor;
    BubbleDeformSmoothing = fresh.BubbleDeformSmoothing;
    BubbleRigid = fresh.BubbleRigid;

    BlurBackground = fresh.BlurBackground;
    BackgroundBlurRadius = fresh.BackgroundBlurRadius;
    BlurOutsideFlashlight = fresh.BlurOutsideFlashlight;
    OutsideFlashlightBlurRadius = fresh.OutsideFlashlightBlurRadius;

    FlashlightFisheye = fresh.FlashlightFisheye;
    FisheyeStrength = fresh.FisheyeStrength;
    FlashlightClearGlass = fresh.FlashlightClearGlass;
    ClearGlassZoom = fresh.ClearGlassZoom;

    PrintEffect = fresh.PrintEffect;
  }

  private static string StripBlockComments(string s)
  {
    var sb = new StringBuilder(s.Length);
    int i = 0;
    while (i < s.Length)
    {
      if (i + 3 < s.Length && s[i] == '-' && s[i + 1] == '-'
          && s[i + 2] == '[' && s[i + 3] == '[')
      {
        int end = s.IndexOf("]]", i + 4, StringComparison.Ordinal);
        if (end < 0) break;
        i = end + 2;
        continue;
      }
      sb.Append(s[i++]);
    }
    return sb.ToString();
  }

  private static string StripLineComment(string line)
  {
    int dashes = line.IndexOf("--", StringComparison.Ordinal);
    int hash = line.IndexOf('#'); // backward-compat com formato antigo
    int cut = -1;
    if (dashes >= 0 && (hash < 0 || dashes < hash)) cut = dashes;
    else if (hash >= 0) cut = hash;
    return cut >= 0 ? line[..cut] : line;
  }

  private static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);

  private static bool ParseBool(string s) =>
       s.Equals("true", StringComparison.OrdinalIgnoreCase)
    || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
    || s.Equals("on", StringComparison.OrdinalIgnoreCase)
    || s == "1";

  private static string ParseString(string s)
  {
    if (s.Length >= 2 && (s[0] == '"' || s[0] == '\'') && s[^1] == s[0])
      return s[1..^1];
    return s;
  }
}
