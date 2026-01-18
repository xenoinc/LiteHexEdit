using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using Lite.HexEditor.Properties;

namespace Lite.HexEditor
{
    public partial class RecentFileHandler : Component
    {
        public class FileMenuItem : ToolStripMenuItem
        {
            string _fileName;

            [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
            public string FileName
            {
                get { return _fileName; }
                set { _fileName = value; }
            }

            public FileMenuItem(string fileName)
            {
                this._fileName = fileName;
            }

            public override string Text
            {
                get
                {
                    ToolStripMenuItem parent = (ToolStripMenuItem)this.OwnerItem;
                    int index = parent.DropDownItems.IndexOf(this);
                    return string.Format("{0} {1}", index+1, _fileName);
                }
                set
                {
                }
            }
        }

        public const int MaxRecentFiles = 24;

        public RecentFileHandler()
        {
            InitializeComponent();

            Init();
        }

        public RecentFileHandler(IContainer container)
        {
            container.Add(this);

            InitializeComponent();

            Init();
        }

        void Init()
        {
            Settings.Default.PropertyChanged += new PropertyChangedEventHandler(Default_PropertyChanged);
        }

        public void AddFile(string fileName)
        {
            if (this._recentFileToolStripItem == null)
                throw new OperationCanceledException("recentFileToolStripItem can not be null!");

            // check if the file is already in the collection
            int alreadyIn = GetIndexOfRecentFile(fileName);
            if (alreadyIn > 0) // remove it
            {
                Settings.Default.RecentFiles.RemoveAt(alreadyIn);
                if(_recentFileToolStripItem.DropDownItems.Count > alreadyIn)
                    _recentFileToolStripItem.DropDownItems.RemoveAt(alreadyIn);
            }
            else if (alreadyIn == 0) // it´s the latest file so return
            {
                return;
            }

            // insert the file on top of the list
            Settings.Default.RecentFiles.Insert(0, fileName);
            _recentFileToolStripItem.DropDownItems.Insert(0, new FileMenuItem(fileName));

            // remove the last one, if max size is reached
            if (Settings.Default.RecentFiles.Count > MaxRecentFiles)
                Settings.Default.RecentFiles.RemoveAt(MaxRecentFiles);
            if (Settings.Default.RecentFiles.Count > Settings.Default.RecentFilesMax)
                this._recentFileToolStripItem.DropDownItems.RemoveAt(Settings.Default.RecentFilesMax);

            // enable the menu item if it´s disabled
            if (!_recentFileToolStripItem.Enabled)
                _recentFileToolStripItem.Enabled = true;

            // save the changes
            Settings.Default.Save();
        }

        int GetIndexOfRecentFile(string filename)
        {
            for (int i = 0; i < Settings.Default.RecentFiles.Count; i++)
            {
                string currentFile = Settings.Default.RecentFiles[i];
                if (string.Equals(currentFile, filename, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        ToolStripMenuItem _recentFileToolStripItem;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ToolStripMenuItem RecentFileToolStripItem
        {
            get { return _recentFileToolStripItem; }
            set 
            {
                if (_recentFileToolStripItem == value)
                    return;

                _recentFileToolStripItem = value;

                ReCreateItems();
            }
        }

        void Default_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "RecentFilesMax")
            {
                ReCreateItems();
            }
        }

        void ReCreateItems()
        {
            if (_recentFileToolStripItem == null)
                return;

            if (Settings.Default.RecentFiles == null)
                Settings.Default.RecentFiles = new StringCollection();

            _recentFileToolStripItem.DropDownItems.Clear();
            _recentFileToolStripItem.Enabled = (Settings.Default.RecentFiles.Count > 0);

            int fileItemCount = Math.Min(Settings.Default.RecentFilesMax, Settings.Default.RecentFiles.Count);
            for(int i = 0; i < fileItemCount; i++)
            {
                string file = Settings.Default.RecentFiles[i];
                _recentFileToolStripItem.DropDownItems.Add(new FileMenuItem(file));
            }
        }

        public void Clear()
        {
            Settings.Default.RecentFiles.Clear();
            ReCreateItems();
        }
    }
}
