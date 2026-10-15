using System.Numerics;
using Coomer.Features.Drawing;
using Coomer.Features.Lighting;
using Coomer.Features.Capture;
using Coomer.Features.Stickers;
using Coomer.Features.Text;

namespace Coomer.Features.Hud;

public sealed class HudRenderer
{
  private readonly TextRenderer _text;

  public bool Visible = true;

  public HudRenderer(TextRenderer text)
  {
    _text = text;
  }

  private const int Size = 14;
  private static readonly Vector4 Fg = new(0.95f, 0.95f, 0.95f, 0.95f);
  private static readonly Vector4 Bg = new(0f, 0f, 0f, 0.55f);
  private static readonly Vector2[] Halo =
  {
    new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
    new(-1, -1), new(1, 1), new(1, -1), new(-1, 1),
  };

  // Iosevka e mono, entao alinhar por espaco ja da colunas certas.
  private static readonly string[] HelpLines =
  {
    "ATALHOS · F1 fecha",
    "",
    "scroll  = -     zoom          arrastar   mover",
    "0               reset         m          espelhar",
    "f               lanterna      ctrl+scroll raio / espessura",
    "c               conta-gotas   b          copiar regiao",
    "ctrl+s          salvar PNG    r          recarregar config",
    "",
    "d               desenhar      s          trocar forma",
    ",               cor           [ ]        espessura",
    "shift           45° / quadrado / circulo perfeito",
    "z               desfazer      shift+z    refazer",
    "x               limpar        v          esconder",
    "t               stamp #       w          texto",
    "y               stickers      espaco     mover no draw",
    "",
    "esc  q          fechar (coomer continua rodando)",
  };

  public void Draw(Vector2 windowSize, DrawTool draw, ColorPicker picker,
                   RegionExporter exporter, StickerState stickerState, bool showHelp)
  {
    if (!Visible) return;

    if (showHelp) DrawHelp(windowSize);

    string? line = BuildLine(draw, picker, exporter, stickerState);
    if (line == null) return;

    var (w, _) = _text.Measure(line, Size);
    float pad = 8f;
    DrawWithHalo(line, new Vector2(windowSize.X - w - pad - 6f, pad + 6f), windowSize);
  }

  private void DrawHelp(Vector2 windowSize)
  {
    var pos = new Vector2(24f, 24f);
    foreach (var line in HelpLines)
    {
      if (line.Length > 0) DrawWithHalo(line, pos, windowSize);
      pos.Y += Size * 1.5f;
    }
  }

  // sombra-fundo: o mesmo texto em preto deslocado em volta faz um halo
  private void DrawWithHalo(string line, Vector2 pos, Vector2 windowSize)
  {
    foreach (var off in Halo)
      _text.Draw(line, Size, pos + off, windowSize, Bg);
    _text.Draw(line, Size, pos, windowSize, Fg);
  }

  private static string? BuildLine(DrawTool draw, ColorPicker picker,
                                   RegionExporter exporter, StickerState stickerState)
  {
    if (exporter.LastStatus is { } status) return status;
    if (exporter.Active) return "REGION COPY · arrasta pra selecionar";
    if (picker.IsEnabled) return picker.LastHex.Length > 0 ? $"PICK · ultimo: {picker.LastHex}" : "PICK · click num pixel";
    if (!draw.IsEnabled) return null;

    if (draw.StickerMode)
    {
      if (draw.SelectedStickerIndex >= 0 && draw.SelectedStickerIndex < draw.StickerStamps.Count)
      {
        var sel = draw.StickerStamps[draw.SelectedStickerIndex];
        var n = System.IO.Path.GetFileName(sel.Path);
        float deg = sel.Rotation * 180f / MathF.PI;
        return $"STICKER SEL · {n} · {(int)(sel.HalfSize * 2)}px · {(int)deg}°";
      }
      var cur = stickerState.Current;
      var name = cur != null ? System.IO.Path.GetFileName(cur.Path) : "(vazio)";
      var flag = draw.StickerMirror ? " <-> " : " ";
      return $"STICKER ·{flag}{name} · {(int)draw.StickerSize}px";
    }

    if (draw.TextMode)
    {
      if (draw.ActiveText != null)
      {
        var preview = draw.ActiveText.Text.Length == 0 ? "(digite)" : draw.ActiveText.Text;
        if (preview.Length > 32) preview = preview[..32] + "...";
        return $"TEXTO · {preview} · {draw.TextFontSize}px";
      }
      return $"TEXTO · click pra comecar · {draw.TextFontSize}px";
    }

    if (draw.StampMode)
      return $"STAMP # · proximo: {draw.NextStampNumber} · raio: {(int)(MathF.Max(12f, draw.Thickness * 2.5f))}px";

    var shapeLabel = draw.Shape switch
    {
      DrawShape.Free => "pen",
      DrawShape.Line => "linha",
      DrawShape.Arrow => "seta",
      DrawShape.Rect => "retangulo",
      DrawShape.Circle => "circulo",
      DrawShape.Highlighter => "marca-texto",
      _ => "?",
    };
    string colorTag = ColorName(draw.ColorIndex);
    return $"DRAW · {shapeLabel} · {colorTag} · {(int)draw.Thickness}px"
         + (draw.Hide ? " · OCULTO" : "");
  }

  private static string ColorName(int idx) => idx switch
  {
    0 => "vermelho",
    1 => "amarelo",
    2 => "verde",
    3 => "azul",
    4 => "magenta",
    5 => "branco",
    6 => "preto",
    _ => "?",
  };
}
