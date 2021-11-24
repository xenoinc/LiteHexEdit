using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Lite.HexEditor.Properties;

namespace Lite.HexEditor
{
  internal class Program
  {
    public const string SoftwareName = "Lite.HexEditor";

    public static FormHexEditor ApplictionForm;

    public static DialogResult ShowError(Exception ex)
    {
      return ShowError(ex.Message);
    }

    public static void ShowMessage(string text)
    {
      MessageBox.Show(text, SoftwareName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static DialogResult ShowQuestion(string text)
    {
      DialogResult result = MessageBox.Show(text, SoftwareName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
      return result;
    }

    internal static DialogResult ShowError(string text)
    {
      DialogResult result = MessageBox.Show(text, SoftwareName, MessageBoxButtons.OK, MessageBoxIcon.Error);
      return result;
    }

    [STAThread()]
    private static void Main(string[] args)
    {
      if (!Settings.Default.UseSystemLanguage)
      {
        string l = Settings.Default.SelectedLanguage;
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(l);
        Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(l);
      }

      Application.EnableVisualStyles();
      Application.SetHighDpiMode(HighDpiMode.SystemAware);
      Application.SetCompatibleTextRenderingDefault(false);

      ApplictionForm = new FormHexEditor();
      if (args.Length > 0 && System.IO.File.Exists(args[0]))
        ApplictionForm.OpenFile(args[0]);

      Application.Run(ApplictionForm);
    }
  }
}
