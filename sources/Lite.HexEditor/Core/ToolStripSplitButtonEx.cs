using System.ComponentModel;
using System.Windows.Forms;

namespace Lite.HexEditor.Core
{
  public class ToolStripSplitButtonEx : ToolStripSplitButton, IScalingItem
  {
    private System.Drawing.Image _image16;
    private System.Drawing.Image _image24;
    private System.Drawing.Image _image32;

    [DefaultValue(null)]
    public System.Drawing.Image Image16
    {
      get => _image16;
      set => _image16 = value;
    }

    [DefaultValue(null)]
    public System.Drawing.Image Image24
    {
      get => _image24;
      set => _image24 = value;
    }

    [DefaultValue(null)]
    public System.Drawing.Image Image32
    {
      get => _image32;
      set => _image32 = value;
    }
  }
}
