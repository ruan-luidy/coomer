using System.Globalization;

namespace Coomer.Features.Configuration;

// Config simples chave=valor em %APPDATA%/coomer/config.
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

  public static Config Default() => new();

  public static Config Load(string path)
  {
    var config = Default();
    foreach (var raw in File.ReadLines(path))
    {
      var line = raw.Trim();
      if (line.Length == 0 || line[0] == '#')
        continue;

      var idx = line.IndexOf('=');
      if (idx < 0)
        continue;

      var key = line[..idx].Trim();
      var value = line[(idx + 1)..].Trim();
      var hash = value.IndexOf('#');
      if (hash >= 0) value = value[..hash].Trim();

      switch (key)
      {
        case "min_scale": config.MinScale = ParseFloat(value); break;
        case "scroll_speed": config.ScrollSpeed = ParseFloat(value); break;
        case "drag_friction": config.DragFriction = ParseFloat(value); break;
        case "scale_friction": config.ScaleFriction = ParseFloat(value); break;

        case "camera_pan_amount": config.CameraPanAmount  = ParseFloat(value); break;
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

        default: throw new InvalidDataException($"Chave de config desconhecida `{key}`");
      }
    }
    return config;
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
  }

  public void Save(string path)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    var c = CultureInfo.InvariantCulture;
    using var w = new StreamWriter(path);
    w.WriteLine($"min_scale = {MinScale.ToString(c)}");
    w.WriteLine($"scroll_speed = {ScrollSpeed.ToString(c)}");
    w.WriteLine($"drag_friction = {DragFriction.ToString(c)}");
    w.WriteLine($"scale_friction = {ScaleFriction.ToString(c)}");

    w.WriteLine($"camera_pan_amount = {CameraPanAmount.ToString(c)}");
    w.WriteLine($"lerp_camera_recenter = {(LerpCameraRecenter ? "true" : "false")}");
    w.WriteLine($"camera_recenter_lerp_speed = {CameraRecenterLerpSpeed.ToString(c)}");
    w.WriteLine($"hide_cursor_on_flashlight = {(HideCursorOnFlashlight ? "true" : "false")}");
    w.WriteLine($"pan_inertia = {(PanInertia ? "true" : "false")}");

    w.WriteLine($"bubble_mass = {BubbleMass.ToString(c)}");
    w.WriteLine($"bubble_spring_k = {BubbleSpringK.ToString(c)}");
    w.WriteLine($"bubble_damping = {BubbleDamping.ToString(c)}");
    w.WriteLine($"bubble_stretch_factor = {BubbleStretchFactor.ToString(c)}");
    w.WriteLine($"bubble_squeeze_factor = {BubbleSqueezeFactor.ToString(c)}");
    w.WriteLine($"bubble_deform_smoothing = {BubbleDeformSmoothing.ToString(c)}");
    w.WriteLine($"bubble_rigid = {(BubbleRigid ? "true" : "false")}");

    w.WriteLine($"blur_background = {(BlurBackground ? "true" : "false")}");
    w.WriteLine($"background_blur_radius = {BackgroundBlurRadius.ToString(c)}");
    w.WriteLine($"blur_outside_flashlight = {(BlurOutsideFlashlight ? "true" : "false")}");
    w.WriteLine($"outside_flashlight_blur_radius = {OutsideFlashlightBlurRadius.ToString(c)}");

    w.WriteLine($"flashlight_fisheye = {(FlashlightFisheye ? "true" : "false")}");
    w.WriteLine($"fisheye_strength = {FisheyeStrength.ToString(c)}");
    w.WriteLine($"flashlight_clear_glass = {(FlashlightClearGlass ? "true" : "false")}");
    w.WriteLine($"clear_glass_zoom = {ClearGlassZoom.ToString(c)}");
  }

  private static float ParseFloat(string s) => float.Parse(s, CultureInfo.InvariantCulture);
  private static bool ParseBool(string s) =>
     s.Equals("true", StringComparison.OrdinalIgnoreCase)
  || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
  || s.Equals("on", StringComparison.OrdinalIgnoreCase)
  || s == "1";
}