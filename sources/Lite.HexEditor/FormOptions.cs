using System;
using System.Data;
using System.Globalization;
using System.Windows.Forms;
using Lite.HexEditor.Properties;

namespace Lite.HexEditor
{
  public partial class FormOptions : Core.FormEx
  {
    private int _recentFilesMax;
    private bool _useSystemLanguage;

    public FormOptions()
    {
      InitializeComponent();

      _recentFilesMax = Settings.Default.RecentFilesMax;
      recentFilesMaxTextBox.DataBindings.Add("Text", this, "RecentFilesMax");
      _useSystemLanguage = Settings.Default.UseSystemLanguage;
      useSystemLanguageCheckBox.DataBindings.Add("Checked", this, "UseSystemLanguage");

      if (string.IsNullOrEmpty(Settings.Default.SelectedLanguage))
        Settings.Default.SelectedLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

      DataTable dt = new DataTable();
      dt.Columns.Add("Name", typeof(string));
      dt.Columns.Add("Value", typeof(string));
      dt.Rows.Add(strings.English, "en");
      dt.Rows.Add(strings.German, "de");
      dt.Rows.Add(strings.Italian, "it");
      dt.Rows.Add(strings.Japanese, "ja");
      dt.Rows.Add(strings.Russian, "ru");
      dt.Rows.Add(strings.SimplifiedChinese, "zh-CN");
      dt.DefaultView.Sort = "Name";

      languageComboBox.DataSource = dt.DefaultView;
      languageComboBox.DisplayMember = "Name";
      languageComboBox.ValueMember = "Value";
      languageComboBox.SelectedValue = Settings.Default.SelectedLanguage;
      if (languageComboBox.SelectedIndex == -1)
        languageComboBox.SelectedIndex = 0;
    }

    public int RecentFilesMax
    {
      get => _recentFilesMax;
      set
      {
        if (_recentFilesMax == value)
          return;

        if (value < 0 || value > RecentFileHandler.MaxRecentFiles)
          return;

        _recentFilesMax = value;
      }
    }

    public bool UseSystemLanguage
    {
      get =>_useSystemLanguage;
      set => _useSystemLanguage = value;
    }

    private void clearRecentFilesButton_Click(object sender, EventArgs e)
    {
      Program.ApplictionForm.RecentFileHandler.Clear();
    }

    private void okButton_Click(object sender, EventArgs e)
    {
      bool changed = false;
      if (_recentFilesMax != Settings.Default.RecentFilesMax)
      {
        Settings.Default.RecentFilesMax = _recentFilesMax;
        changed = true;
      }

      if (Settings.Default.UseSystemLanguage != _useSystemLanguage ||
          Settings.Default.SelectedLanguage != (string)languageComboBox.SelectedValue)
      {
        Settings.Default.UseSystemLanguage = UseSystemLanguage;
        Settings.Default.SelectedLanguage = (string)languageComboBox.SelectedValue;

        Program.ShowMessage(strings.ProgramRestartSettings);

        changed = true;
      }

      if (changed)
        Settings.Default.Save();

      DialogResult = DialogResult.OK;
    }

    private void useSystemLanguageCheckBox_CheckedChanged(object sender, EventArgs e)
    {
      languageComboBox.Enabled = selectLanguageLabel.Enabled = !useSystemLanguageCheckBox.Checked;
    }
  }
}
