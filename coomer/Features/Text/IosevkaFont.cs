using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Coomer.Features.Text;

public static class IosevkaFont
{
  private static readonly PrivateFontCollection _collection = new();
  private static FontFamily? _family;
  private static readonly Dictionary<float, Font> _cache = new();

  public static FontFamily Family
  {
    get
    {
      if (_family != null) return _family;
      var asm = Assembly.GetExecutingAssembly();
      using var stream = asm.GetManifestResourceStream("Coomer.Resources.Iosevka.ttf")
        ?? throw new FileNotFoundException("Iosevka.ttf nao embutido — coloca em coomer/Resources/Iosevka.ttf");
      var bytes = new byte[stream.Length];
      int read = 0;
      while (read < bytes.Length)
        read += stream.Read(bytes, read, bytes.Length - read);
      var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
      try { _collection.AddMemoryFont(handle.AddrOfPinnedObject(), bytes.Length); }
      finally { handle.Free(); }
      _family = _collection.Families[0];
      return _family;
    }
  }

  public static Font Get(float sizePx)
  {
    if (_cache.TryGetValue(sizePx, out var f)) return f;
    f = new Font(Family, sizePx, FontStyle.Regular, GraphicsUnit.Pixel);
    _cache[sizePx] = f;
    return f;
  }
}
