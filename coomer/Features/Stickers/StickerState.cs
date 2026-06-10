namespace Coomer.Features.Stickers;

public sealed class StickerState
{
  public StickerEntry? Current;
  public int CategoryIndex;
  public int IndexInCategory;

  public void CycleSticker(StickerCache cache, int delta)
  {
    if (cache.Categories.Count == 0) { Current = null; return; }
    var cat = cache.Categories[Math.Clamp(CategoryIndex, 0, cache.Categories.Count - 1)];
    var list = cache.InCategory(cat);
    if (list.Count == 0) { Current = null; return; }
    IndexInCategory = ((IndexInCategory + delta) % list.Count + list.Count) % list.Count;
    Current = list[IndexInCategory];
  }

  public void CycleCategory(StickerCache cache, int delta)
  {
    if (cache.Categories.Count == 0) { Current = null; return; }
    CategoryIndex = ((CategoryIndex + delta) % cache.Categories.Count + cache.Categories.Count) % cache.Categories.Count;
    IndexInCategory = 0;
    var cat = cache.Categories[CategoryIndex];
    var list = cache.InCategory(cat);
    Current = list.Count > 0 ? list[0] : null;
  }

  public void RefreshFrom(StickerCache cache)
  {
    if (Current != null)
    {
      var stillThere = cache.All.FirstOrDefault(e => e.Path == Current.Path);
      if (stillThere != null) { Current = stillThere; return; }
    }
    CategoryIndex = 0;
    IndexInCategory = 0;
    if (cache.Categories.Count > 0)
    {
      var list = cache.InCategory(cache.Categories[0]);
      Current = list.Count > 0 ? list[0] : null;
    }
    else Current = null;
  }
}
