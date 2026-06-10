using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Coomer.Features.Stickers;

public sealed class StickerEntry
{
  public string Path = "";
  public string Category = "";
  public uint Texture;
  public int Width;
  public int Height;
}

public sealed unsafe class StickerCache : IDisposable
{
  private readonly GL _gl;
  private readonly string _root;
  private readonly Dictionary<string, List<StickerEntry>> _byCategory = new();
  private readonly List<string> _categoryOrder = new();
  private readonly List<StickerEntry> _all = new();

  public IReadOnlyList<string> Categories => _categoryOrder;
  public IReadOnlyList<StickerEntry> All => _all;

  public StickerCache(GL gl, string root)
  {
    _gl = gl;
    _root = root;
  }

  public IReadOnlyList<StickerEntry> InCategory(string category)
    => _byCategory.TryGetValue(category, out var list) ? list : Array.Empty<StickerEntry>();

  public void Reload()
  {
    foreach (var e in _all) _gl.DeleteTexture(e.Texture);
    _all.Clear();
    _byCategory.Clear();
    _categoryOrder.Clear();

    try { Directory.CreateDirectory(_root); }
    catch { return; }

    var files = SafeEnumerate(_root);
    foreach (var path in files)
    {
      var ext = Path.GetExtension(path).ToLowerInvariant();
      if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif"))
        continue;

      var entry = TryLoad(path);
      if (entry == null) continue;
      _all.Add(entry);

      if (!_byCategory.TryGetValue(entry.Category, out var list))
      {
        list = new List<StickerEntry>();
        _byCategory[entry.Category] = list;
        _categoryOrder.Add(entry.Category);
      }
      list.Add(entry);
    }
  }

  private StickerEntry? TryLoad(string path)
  {
    try
    {
      using var image = Image.Load<Rgba32>(path);
      var pixels = new byte[image.Width * image.Height * 4];
      image.CopyPixelDataTo(pixels);

      uint tex = _gl.GenTexture();
      _gl.BindTexture(TextureTarget.Texture2D, tex);
      fixed (byte* p = pixels)
      {
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Rgba8,
            (uint)image.Width, (uint)image.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, p);
      }
      _gl.GenerateMipmap(TextureTarget.Texture2D);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
      _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

      var relDir = Path.GetRelativePath(_root, Path.GetDirectoryName(path) ?? _root);
      if (relDir == ".") relDir = "";
      return new StickerEntry
      {
        Path = path,
        Category = relDir.Replace('\\', '/'),
        Texture = tex,
        Width = image.Width,
        Height = image.Height,
      };
    }
    catch
    {
      return null;
    }
  }

  private static IEnumerable<string> SafeEnumerate(string root)
  {
    try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
    catch { return Array.Empty<string>(); }
  }

  public void Dispose()
  {
    foreach (var e in _all) _gl.DeleteTexture(e.Texture);
    _all.Clear();
    _byCategory.Clear();
    _categoryOrder.Clear();
  }
}
