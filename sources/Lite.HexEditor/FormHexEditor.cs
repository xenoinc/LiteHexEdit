using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Lite.Windows.Forms;

namespace Lite.HexEditor
{
  public partial class FormHexEditor : Core.FormEx
  {
    private FormFind _formFind;
    private FindOptions _findOptions = new FindOptions();
    private FormGoTo _formGoto = new FormGoTo();
    private string _fileName;

    public FormHexEditor()
    {
      InitializeComponent();

      Init();

      hexBox.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, SystemFonts.MessageBoxFont.Size, SystemFonts.MessageBoxFont.Style);

      this.toolStrip.Renderer.RenderToolStripBorder += new ToolStripRenderEventHandler(Renderer_RenderToolStripBorder);
    }

    /// <summary>
    /// Removes the border on the right of the tool strip
    /// </summary>
    /// <param name="sender">the renderer</param>
    /// <param name="e">the event args</param>
    private void Renderer_RenderToolStripBorder(object sender, ToolStripRenderEventArgs e)
    {
      if (e.ToolStrip.GetType() != typeof(ToolStrip))
        return;

      e.Graphics.DrawLine(new Pen(new SolidBrush(SystemColors.Control)), new Point(toolStrip.Width - 1, 0),
        new Point(toolStrip.Width - 1, toolStrip.Height));
    }

    /// <summary>
    /// Initializes the hex editor�s main form
    /// </summary>
    private void Init()
    {
      DisplayText();

      ManageAbility();

      UpdateBitControlVisibility();

      //var selected = ;
      var defConverter = new DefaultByteCharConverter();
      ToolStripMenuItem miDefault = new ToolStripMenuItem();
      miDefault.Text = defConverter.ToString();
      miDefault.Tag = defConverter;
      miDefault.Click += new EventHandler(encodingMenuItem_Clicked);

      var altConverter = new UtfByteCharProvider();
      ////var converter = new EbcdicByteCharProvider();
      ToolStripMenuItem miEbcdic = new ToolStripMenuItem();
      miEbcdic.Text = altConverter.ToString();
      miEbcdic.Tag = altConverter;
      miEbcdic.Click += new EventHandler(encodingMenuItem_Clicked);

      encodingToolStripComboBox.Items.Add(defConverter);
      encodingToolStripComboBox.Items.Add(altConverter);

      encodingToolStripMenuItem.DropDownItems.Add(miDefault);
      encodingToolStripMenuItem.DropDownItems.Add(miEbcdic);
      encodingToolStripComboBox.SelectedIndex = 0;

      UpdateFormWidth();
    }

    private void encodingMenuItem_Clicked(object sender, EventArgs e)
    {
      var converter = ((ToolStripMenuItem)sender).Tag;
      encodingToolStripComboBox.SelectedItem = converter;
    }

    /// <summary>
    /// Updates the File size status label
    /// </summary>
    private void UpdateFileSizeStatus()
    {
      if (this.hexBox.ByteProvider == null)
        this.fileSizeToolStripStatusLabel.Text = string.Empty;
      else
        this.fileSizeToolStripStatusLabel.Text = Util.GetDisplayBytes(this.hexBox.ByteProvider.Length);
    }

    /// <summary>
    /// Displays the file name in the Form�s text property
    /// </summary>
    /// <param name="fileName">the file name to display</param>
    private void DisplayText()
    {
      if (_fileName != null && _fileName.Length > 0)
      {
        string textFormat = "{0}{1} - {2}";
        string readOnly = ((DynamicFileByteProvider)hexBox.ByteProvider).ReadOnly
            ? strings.Readonly : "";
        string text = Path.GetFileName(_fileName);
        this.Text = string.Format(textFormat, text, readOnly, Program.SoftwareName);
      }
      else
      {
        this.Text = Program.SoftwareName;
      }
    }

    /// <summary>
    /// Manages enabling or disabling of menu items and toolstrip buttons.
    /// </summary>
    private void ManageAbility()
    {
      if (hexBox.ByteProvider == null)
      {
        saveToolStripMenuItem.Enabled = saveToolStripButton.Enabled = false;

        findToolStripMenuItem.Enabled = false;
        findNextToolStripMenuItem.Enabled = false;
        goToToolStripMenuItem.Enabled = false;

        selectAllToolStripMenuItem.Enabled = false;
      }
      else
      {
        saveToolStripMenuItem.Enabled = saveToolStripButton.Enabled = hexBox.ByteProvider.HasChanges();

        findToolStripMenuItem.Enabled = true;
        findNextToolStripMenuItem.Enabled = true;
        goToToolStripMenuItem.Enabled = true;

        selectAllToolStripMenuItem.Enabled = true;
      }

      ManageAbilityForCopyAndPaste();
    }

    /// <summary>
    /// Manages enabling or disabling of menustrip items and toolstrip buttons for copy and paste
    /// </summary>
    private void ManageAbilityForCopyAndPaste()
    {
      copyHexStringToolStripMenuItem.Enabled =
          copyToolStripSplitButton.Enabled = copyToolStripMenuItem.Enabled = hexBox.CanCopy();

      cutToolStripButton.Enabled = cutToolStripMenuItem.Enabled = hexBox.CanCut();
      pasteToolStripSplitButton.Enabled = pasteToolStripMenuItem.Enabled = hexBox.CanPaste();
      pasteHexToolStripMenuItem.Enabled = pasteHexToolStripMenuItem1.Enabled = hexBox.CanPasteHex();
    }

    /// <summary>
    /// Shows the open file dialog.
    /// </summary>
    private void OpenFile()
    {
      if (openFileDialog.ShowDialog() == DialogResult.OK)
      {
        OpenFile(openFileDialog.FileName);
      }
    }

    /// <summary>
    /// Opens a file.
    /// </summary>
    /// <param name="fileName">the file name of the file to open</param>
    public void OpenFile(string fileName)
    {
      if (!File.Exists(fileName))
      {
        Program.ShowMessage(strings.FileDoesNotExist);
        return;
      }

      if (CloseFile() == DialogResult.Cancel)
        return;

      try
      {
        DynamicFileByteProvider dynamicFileByteProvider;
        try
        {
          // try to open in write mode
          dynamicFileByteProvider = new DynamicFileByteProvider(fileName);
          dynamicFileByteProvider.Changed += new EventHandler(byteProvider_Changed);
          dynamicFileByteProvider.LengthChanged += new EventHandler(byteProvider_LengthChanged);
        }
        catch (IOException) // write mode failed
        {
          try
          {
            // try to open in read-only mode
            dynamicFileByteProvider = new DynamicFileByteProvider(fileName, true);
            if (Program.ShowQuestion(strings.OpenReadonly) == DialogResult.No)
            {
              dynamicFileByteProvider.Dispose();
              return;
            }
          }
          catch (IOException) // read-only also failed
          {
            // file cannot be opened
            Program.ShowError(strings.OpenFailed);
            return;
          }
        }

        hexBox.ByteProvider = dynamicFileByteProvider;
        _fileName = fileName;

        DisplayText();

        UpdateFileSizeStatus();

        RecentFileHandler.AddFile(fileName);
      }
      catch (Exception ex1)
      {
        Program.ShowError(ex1);
        return;
      }
      finally
      {
        ManageAbility();
      }
    }

    /// <summary>
    /// Saves the current file.
    /// </summary>
    private void SaveFile()
    {
      if (hexBox.ByteProvider == null)
        return;

      try
      {
        DynamicFileByteProvider dynamicFileByteProvider = hexBox.ByteProvider as DynamicFileByteProvider;
        dynamicFileByteProvider.ApplyChanges();
      }
      catch (Exception ex1)
      {
        Program.ShowError(ex1);
      }
      finally
      {
        ManageAbility();
      }
    }

    /// <summary>
    /// Closes the current file
    /// </summary>
    /// <returns>OK, if the current file was closed.</returns>
    private DialogResult CloseFile()
    {
      if (hexBox.ByteProvider == null)
        return DialogResult.OK;

      try

      {
        if (hexBox.ByteProvider != null && hexBox.ByteProvider.HasChanges())
        {
          DialogResult res = MessageBox.Show(strings.SaveChangesQuestion,
              Program.SoftwareName,
              MessageBoxButtons.YesNoCancel,
              MessageBoxIcon.Warning);

          if (res == DialogResult.Yes)
          {
            SaveFile();
            CleanUp();
          }
          else if (res == DialogResult.No)
          {
            CleanUp();
          }
          else if (res == DialogResult.Cancel)
          {
            return res;
          }

          return res;
        }
        else
        {
          CleanUp();
          return DialogResult.OK;
        }
      }
      finally
      {
        ManageAbility();
      }
    }

    private void CleanUp()
    {
      if (hexBox.ByteProvider != null)
      {
        IDisposable byteProvider = hexBox.ByteProvider as IDisposable;
        if (byteProvider != null)
          byteProvider.Dispose();
        hexBox.ByteProvider = null;
      }
      _fileName = null;
      DisplayText();
    }

    /// <summary>
    /// Opens the Find dialog
    /// </summary>
    private void Find()
    {
      ShowFind();
    }

    /// <summary>
    /// Creates a new FormFind dialog
    /// </summary>
    /// <returns>the form find dialog</returns>
    private FormFind ShowFind()
    {
      if (_formFind == null || _formFind.IsDisposed)
      {
        _formFind = new FormFind();
        _formFind.HexBox = this.hexBox;
        _formFind.FindOptions = _findOptions;
        _formFind.Show(this);
      }
      else
      {
        _formFind.Focus();
      }
      return _formFind;
    }

    /// <summary>
    /// Find next match
    /// </summary>
    private void FindNext()
    {
      ShowFind().FindNext();
    }

    /// <summary>
    /// Aborts the current find process
    /// </summary>
    private void FormFindCancel_Closed(object sender, EventArgs e)
    {
      hexBox.AbortFind();
    }

    /// <summary>
    /// Displays the goto byte dialog.
    /// </summary>
    private void Goto()
    {
      _formGoto.SetMaxByteIndex(hexBox.ByteProvider.Length);
      _formGoto.SetDefaultValue(hexBox.SelectionStart);
      if (_formGoto.ShowDialog() == DialogResult.OK)
      {
        hexBox.SelectionStart = _formGoto.GetByteIndex();
        hexBox.SelectionLength = 1;
        hexBox.Focus();
      }
    }

    /// <summary>
    /// Enables drag&drop
    /// </summary>
    private void hexBox_DragEnter(object sender, System.Windows.Forms.DragEventArgs e)
    {
      e.Effect = DragDropEffects.All;
    }

    /// <summary>
    /// Processes a file drop
    /// </summary>
    private void hexBox_DragDrop(object sender, System.Windows.Forms.DragEventArgs e)
    {
      object oFileNames = e.Data.GetData(DataFormats.FileDrop);
      string[] fileNames = (string[])oFileNames;
      if (fileNames.Length == 1)
      {
        OpenFile(fileNames[0]);
      }
    }

    private void hexBox_Copied(object sender, EventArgs e)
    {
      ManageAbilityForCopyAndPaste();
    }

    private void hexBox_CopiedHex(object sender, EventArgs e)
    {
      ManageAbilityForCopyAndPaste();
    }

    private void hexBox_SelectionLengthChanged(object sender, System.EventArgs e)
    {
      ManageAbilityForCopyAndPaste();
    }

    private void hexBox_SelectionStartChanged(object sender, System.EventArgs e)
    {
      ManageAbilityForCopyAndPaste();
    }

    private void Position_Changed(object sender, EventArgs e)
    {
      this.toolStripStatusLabel.Text = string.Format("Ln {0}    Col {1}",
          hexBox.CurrentLine, hexBox.CurrentPositionInLine);

      string bitPresentation = string.Empty;

      byte? currentByte = hexBox.ByteProvider != null && hexBox.ByteProvider.Length > hexBox.SelectionStart
        ? hexBox.ByteProvider.ReadByte(hexBox.SelectionStart)
        : (byte?)null;

      BitInfo bitInfo = currentByte != null ? new BitInfo((byte)currentByte, hexBox.SelectionStart) : null;

      if (bitInfo != null)
      {
        byte currentByteNotNull = (byte)currentByte;
        bitPresentation = string.Format("Bits of Byte {0}: {1}"
          , hexBox.SelectionStart
          , bitInfo.ToString()
          );
      }

      this.bitToolStripStatusLabel.Text = bitPresentation;

      this.bitControl1.BitInfo = bitInfo;
    }

    private void byteProvider_Changed(object sender, EventArgs e)
    {
      ManageAbility();
    }

    private void byteProvider_LengthChanged(object sender, EventArgs e)
    {
      UpdateFileSizeStatus();
    }

    private void open_Click(object sender, EventArgs e)
    {
      OpenFile();
    }

    private void save_Click(object sender, EventArgs e)
    {
      SaveFile();
    }

    private void cut_Click(object sender, EventArgs e)
    {
      this.hexBox.Cut();
    }

    private void copy_Click(object sender, EventArgs e)
    {
      this.hexBox.Copy();
    }

    private void paste_Click(object sender, EventArgs e)
    {
      this.hexBox.Paste();
    }

    private void copyHex_Click(object sender, EventArgs e)
    {
      this.hexBox.CopyHex();
    }

    private void pasteHex_Click(object sender, EventArgs e)
    {
      this.hexBox.PasteHex();
    }

    private void find_Click(object sender, EventArgs e)
    {
      this.Find();
    }

    private void findNext_Click(object sender, EventArgs e)
    {
      this.FindNext();
    }

    private void goTo_Click(object sender, EventArgs e)
    {
      this.Goto();
    }

    private void selectAllToolStripMenuItem_Click(object sender, EventArgs e)
    {
      this.hexBox.SelectAll();
    }

    private void exit_Click(object sender, EventArgs e)
    {
      this.Close();
    }

    private void about_Click(object sender, EventArgs e)
    {
      new FormAbout().ShowDialog();
    }

    private void recentFiles_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {
      RecentFileHandler.FileMenuItem fmi = (RecentFileHandler.FileMenuItem)e.ClickedItem;
      this.OpenFile(fmi.FileName);
    }

    private void options_Click(object sender, EventArgs e)
    {
      new FormOptions().ShowDialog();
    }

    private void FormHexEditor_FormClosing(object sender, FormClosingEventArgs e)
    {
      var result = CloseFile();
      if (result == DialogResult.Cancel)
        e.Cancel = true;
    }

    private void toolStripEncoding_SelectedIndexChanged(object sender, EventArgs e)
    {
      hexBox.ByteCharConverter = encodingToolStripComboBox.SelectedItem as IByteCharConverter;

      foreach (ToolStripMenuItem encodingMenuItem in encodingToolStripMenuItem.DropDownItems)
        encodingMenuItem.Checked = (encodingMenuItem.Tag == hexBox.ByteCharConverter);
    }

    private void bitsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
    {
      UpdateBitControlVisibility();
    }

    private void UpdateBitControlVisibility()
    {
      if (Util.DesignMode)
        return;
      //if (this.bitControl1.Visible == bitsToolStripMenuItem.Checked)
      //{
      //    return;
      //}
      if (bitsToolStripMenuItem.Checked)
      {
        hexBox.Height -= bitControl1.Height;
        bitControl1.Visible = true;
      }
      else
      {
        hexBox.Height += bitControl1.Height;
        bitControl1.Visible = false;
      }
    }

    private void bitControl1_BitChanged(object sender, EventArgs e)
    {
      hexBox.ByteProvider.WriteByte(bitControl1.BitInfo.Position, bitControl1.BitInfo.Value);
      hexBox.Invalidate();
    }

    private void menuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
    {
    }

    private void hexBox_RequiredWidthChanged(object sender, EventArgs e)
    {
      UpdateFormWidth();
    }

    private void UpdateFormWidth()
    {
      this.Width = this.hexBox.RequiredWidth + 70;
    }
  }
}