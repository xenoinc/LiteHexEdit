using System.Drawing;
using System.Windows.Forms;

namespace Lite.HexEditor.Core
{
  public static class CoreUtil
  {
    public static void AdjustImages(ToolStrip toolStrip, ref float dpiOld, float dpiNew)
    {
      if (Util.DesignMode) return;

      var factor = dpiNew / dpiOld;

      if ((dpiNew == 0) || (dpiOld == dpiNew))
        return;

      dpiOld = dpiNew;

      toolStrip.ImageScalingSize = new System.Drawing.Size((int)(toolStrip.ImageScalingSize.Width * factor), (int)(toolStrip.ImageScalingSize.Height * factor));

      var width = toolStrip.ImageScalingSize.Width;

      foreach (ToolStripItem item in toolStrip.Items)
      {
        var scalingItem = item as IScalingItem;
        if (scalingItem == null)
          continue;

        if (width < 17 && scalingItem.Image16 != null)
          item.Image = scalingItem.Image16;
        else if (width < 25 && scalingItem.Image24 != null)
          item.Image = scalingItem.Image24;
        else if (width < 33 && scalingItem.Image32 != null)
          item.Image = scalingItem.Image32;
      }
    }

    public static T GetParent<T>(Control c) where T : Control
    {
      if (c == null)
        return default(T);

      var parent = c.Parent;

      if (parent is T found)
        return found;

      return GetParent<T>(parent);
    }

    public static void ScaleFont(Control control, float factor)
    {
      control.Font = new Font(control.Font.FontFamily,
                           control.Font.Size * factor,
                           control.Font.Style);
    }
  }
}
