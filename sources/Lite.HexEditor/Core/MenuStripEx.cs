using System;
using System.Windows.Forms;

namespace Lite.HexEditor.Core
{
  public class MenuStripEx : MenuStrip
  {
    private float _dpiOld = 96F;
    private FormEx _form;

    public MenuStripEx()
    {
    }

    protected override void OnHandleCreated(EventArgs e)
    {
      base.OnHandleCreated(e);
      EnableFormEvents();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
      base.OnHandleDestroyed(e);
      EnableFormEvents();
    }

    protected override void OnParentChanged(EventArgs e)
    {
      base.OnParentChanged(e);

      EnableFormEvents();
    }

    protected override void ScaleControl(System.Drawing.SizeF factor, BoundsSpecified specified)
    {
      base.ScaleControl(factor, specified);
      CoreUtil.AdjustImages(this, ref _dpiOld, _form.DpiNew);
    }

    private void EnableFormEvents()
    {
      _form = CoreUtil.GetParent<FormEx>(this);

      if (_form != null)
        CoreUtil.AdjustImages(this, ref _dpiOld, _form.DpiNew);
    }
  }
}
