using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Security.Permissions;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Lite.Windows.Forms.Converters;

namespace Lite.Windows.Forms
{
  /// <summary>
  /// Represents a hex box control.
  /// </summary>
  [ToolboxBitmap(typeof(HexBox), "HexBox.bmp")]
  public class HexBox : Control
  {
    #region IKeyInterpreter interface

    /// <summary>
    /// Defines a user input handler such as for mouse and keyboard input
    /// </summary>
    private interface IKeyInterpreter
    {
      /// <summary>
      /// Activates mouse events
      /// </summary>
      void Activate();

      /// <summary>
      /// Deactivate mouse events
      /// </summary>
      void Deactivate();

      /// <summary>
      /// Gives some information about where to place the caret.
      /// </summary>
      /// <param name="byteIndex">the index of the byte</param>
      /// <returns>the position where the caret is to place.</returns>
      PointF GetCaretPointF(long byteIndex);

      /// <summary>
      /// Preprocesses WM_CHAR window message.
      /// </summary>
      /// <param name="m">the Message object to process.</param>
      /// <returns>True, if the message was processed.</returns>
      bool PreProcessWmChar(ref Message m);

      /// <summary>
      /// Preprocesses WM_KEYDOWN window message.
      /// </summary>
      /// <param name="m">the Message object to process.</param>
      /// <returns>True, if the message was processed.</returns>
      bool PreProcessWmKeyDown(ref Message m);

      /// <summary>
      /// Preprocesses WM_KEYUP window message.
      /// </summary>
      /// <param name="m">the Message object to process.</param>
      /// <returns>True, if the message was processed.</returns>
      bool PreProcessWmKeyUp(ref Message m);
    }

    #endregion IKeyInterpreter interface

    #region EmptyKeyInterpreter class

    /// <summary>
    /// Represents an empty input handler without any functionality.
    /// If is set ByteProvider to null, then this interpreter is used.
    /// </summary>
    private class EmptyKeyInterpreter : IKeyInterpreter
    {
      private HexBox _hexBox;

      public EmptyKeyInterpreter(HexBox hexBox)
      {
        _hexBox = hexBox;
      }

      #region IKeyInterpreter Members

      public void Activate()
      { }

      public void Deactivate()
      { }

      public PointF GetCaretPointF(long byteIndex)
      { return new PointF(); }

      public bool PreProcessWmChar(ref Message m)
      { return _hexBox.BasePreProcessMessage(ref m); }

      public bool PreProcessWmKeyDown(ref Message m)
      { return _hexBox.BasePreProcessMessage(ref m); }

      public bool PreProcessWmKeyUp(ref Message m)
      { return _hexBox.BasePreProcessMessage(ref m); }

      #endregion IKeyInterpreter Members
    }

    #endregion EmptyKeyInterpreter class

    #region KeyInterpreter class

    /// <summary>
    /// Handles user input such as mouse and keyboard input during hex view edit
    /// </summary>
    private class KeyInterpreter : IKeyInterpreter
    {
      /// <summary>
      /// Delegate for key-down processing.
      /// </summary>
      /// <param name="m">the message object contains key data information</param>
      /// <returns>True, if the message was processed</returns>
      private delegate bool MessageDelegate(ref Message m);

      #region Fields

      /// <summary>
      /// Contains the parent HexBox control
      /// </summary>
      protected HexBox _hexBox;

      /// <summary>
      /// Contains True, if shift key is down
      /// </summary>
      protected bool _shiftDown;

      /// <summary>
      /// Contains the current mouse selection position info
      /// </summary>
      private BytePositionInfo _bpi;

      /// <summary>
      /// Contains the selection start position info
      /// </summary>
      private BytePositionInfo _bpiStart;

      /// <summary>
      /// Contains all message handlers of key interpreter key down message
      /// </summary>
      private Dictionary<Keys, MessageDelegate> _messageHandlers;

      /// <summary>
      /// Contains True, if mouse is down
      /// </summary>
      private bool _mouseDown;

      #endregion Fields

      #region Ctors

      public KeyInterpreter(HexBox hexBox)
      {
        _hexBox = hexBox;
      }

      #endregion Ctors

      #region Activate, Deactive methods

      public virtual void Activate()
      {
        _hexBox.MouseDown += new MouseEventHandler(BeginMouseSelection);
        _hexBox.MouseMove += new MouseEventHandler(UpdateMouseSelection);
        _hexBox.MouseUp += new MouseEventHandler(EndMouseSelection);
      }

      public virtual void Deactivate()
      {
        _hexBox.MouseDown -= new MouseEventHandler(BeginMouseSelection);
        _hexBox.MouseMove -= new MouseEventHandler(UpdateMouseSelection);
        _hexBox.MouseUp -= new MouseEventHandler(EndMouseSelection);
      }

      #endregion Activate, Deactive methods

      #region Mouse selection methods

      private void BeginMouseSelection(object sender, MouseEventArgs e)
      {
        System.Diagnostics.Debug.WriteLine("BeginMouseSelection()", "KeyInterpreter");

        if (e.Button != MouseButtons.Left)
          return;

        _mouseDown = true;

        if (!_shiftDown)
        {
          _bpiStart = new BytePositionInfo(_hexBox._bytePos, _hexBox._byteCharacterPos);
          _hexBox.ReleaseSelection();
        }
        else
        {
          UpdateMouseSelection(this, e);
        }
      }

      private void EndMouseSelection(object sender, MouseEventArgs e)
      {
        _mouseDown = false;
      }

      private void UpdateMouseSelection(object sender, MouseEventArgs e)
      {
        if (!_mouseDown)
          return;

        _bpi = GetBytePositionInfo(new Point(e.X, e.Y));
        long selEnd = _bpi.Index;
        long realselStart;
        long realselLength;

        if (selEnd < _bpiStart.Index)
        {
          realselStart = selEnd;
          realselLength = _bpiStart.Index - selEnd;
        }
        else if (selEnd > _bpiStart.Index)
        {
          realselStart = _bpiStart.Index;
          realselLength = selEnd - realselStart;
        }
        else
        {
          realselStart = _hexBox._bytePos;
          realselLength = 0;
        }

        if (realselStart != _hexBox._bytePos || realselLength != _hexBox._selectionLength)
        {
          _hexBox.InternalSelect(realselStart, realselLength);
          _hexBox.ScrollByteIntoView(_bpi.Index);
        }
      }

      #endregion Mouse selection methods

      #region PrePrcessWmKeyDown methods

      public virtual bool PreProcessWmKeyDown(ref Message m)
      {
        System.Diagnostics.Debug.WriteLine("PreProcessWmKeyDown(ref Message m)", "KeyInterpreter");

        Keys vc = (Keys)m.WParam.ToInt32();

        Keys keyData = vc | Control.ModifierKeys;

        // detect whether key down event should be raised
        var hasMessageHandler = this.MessageHandlers.ContainsKey(keyData);
        if (hasMessageHandler && RaiseKeyDown(keyData))
          return true;

        MessageDelegate messageHandler = hasMessageHandler
          ? this.MessageHandlers[keyData]
          : messageHandler = new MessageDelegate(PreProcessWmKeyDown_Default);

        return messageHandler(ref m);
      }

      protected virtual bool PreProcessWmKeyDown_Back(ref Message m)
      {
        if (!_hexBox._byteProvider.SupportsDeleteBytes())
          return true;

        if (_hexBox.ReadOnly)
          return true;

        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;
        int cp = _hexBox._byteCharacterPos;

        long startDelete = (cp == 0 && sel == 0) ? pos - 1 : pos;
        if (startDelete < 0 && sel < 1)
          return true;

        long bytesToDelete = (sel > 0) ? sel : 1;
        _hexBox._byteProvider.DeleteBytes(Math.Max(0, startDelete), bytesToDelete);
        _hexBox.UpdateScrollSize();

        if (sel == 0)
          PerformPosMoveLeftByte();

        _hexBox.ReleaseSelection();
        _hexBox.Invalidate();

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ControlC(ref Message m)
      {
        _hexBox.Copy();
        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ControlV(ref Message m)
      {
        _hexBox.Paste();
        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ControlX(ref Message m)
      {
        _hexBox.Cut();
        return true;
      }

      protected bool PreProcessWmKeyDown_Default(ref Message m)
      {
        _hexBox.ScrollByteIntoView();
        return _hexBox.BasePreProcessMessage(ref m);
      }

      protected virtual bool PreProcessWmKeyDown_Delete(ref Message m)
      {
        if (!_hexBox._byteProvider.SupportsDeleteBytes())
          return true;

        if (_hexBox.ReadOnly)
          return true;

        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;

        if (pos >= _hexBox._byteProvider.Length)
          return true;

        long bytesToDelete = (sel > 0) ? sel : 1;
        _hexBox._byteProvider.DeleteBytes(pos, bytesToDelete);

        _hexBox.UpdateScrollSize();
        _hexBox.ReleaseSelection();
        _hexBox.Invalidate();

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_Down(ref Message m)
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos == _hexBox._byteProvider.Length && cp == 0)
          return true;

        pos = Math.Min(_hexBox._byteProvider.Length, pos + _hexBox._iHexMaxHBytes);

        if (pos == _hexBox._byteProvider.Length)
          cp = 0;

        _hexBox.SetPosition(pos, cp);

        if (pos > _hexBox._endByte - 1)
        {
          _hexBox.PerformScrollLineDown();
        }

        _hexBox.UpdateCaret();
        _hexBox.ScrollByteIntoView();
        _hexBox.ReleaseSelection();
        _hexBox.Invalidate();

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_End(ref Message m)
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos >= _hexBox._byteProvider.Length - 1)
          return true;

        pos = _hexBox._byteProvider.Length;
        cp = 0;
        _hexBox.SetPosition(pos, cp);

        _hexBox.ScrollByteIntoView();
        _hexBox.UpdateCaret();
        _hexBox.ReleaseSelection();

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_Home(ref Message m)
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos < 1)
          return true;

        pos = 0;
        cp = 0;
        _hexBox.SetPosition(pos, cp);

        _hexBox.ScrollByteIntoView();
        _hexBox.UpdateCaret();
        _hexBox.ReleaseSelection();

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_Left(ref Message m)
      {
        return PerformPosMoveLeft();
      }

      protected virtual bool PreProcessWmKeyDown_PageDown(ref Message m)
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos == _hexBox._byteProvider.Length && cp == 0)
          return true;

        pos = Math.Min(_hexBox._byteProvider.Length, pos + _hexBox._iHexMaxBytes);

        if (pos == _hexBox._byteProvider.Length)
          cp = 0;

        _hexBox.SetPosition(pos, cp);

        if (pos > _hexBox._endByte - 1)
        {
          _hexBox.PerformScrollPageDown();
        }

        _hexBox.ReleaseSelection();
        _hexBox.UpdateCaret();
        _hexBox.Invalidate();

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_PageUp(ref Message m)
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos == 0 && cp == 0)
          return true;

        pos = Math.Max(0, pos - _hexBox._iHexMaxBytes);
        if (pos == 0)
          return true;

        _hexBox.SetPosition(pos);

        if (pos < _hexBox._startByte)
        {
          _hexBox.PerformScrollPageUp();
        }

        _hexBox.ReleaseSelection();
        _hexBox.UpdateCaret();
        _hexBox.Invalidate();
        return true;
      }

      protected virtual bool PreProcessWmKeyDown_Right(ref Message m)
      {
        return PerformPosMoveRight();
      }

      protected virtual bool PreProcessWmKeyDown_ShiftDown(ref Message m)
      {
        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;

        long max = _hexBox._byteProvider.Length;

        if (pos + sel + _hexBox._iHexMaxHBytes > max)
          return true;

        if (_bpiStart.Index <= pos)
        {
          sel += _hexBox._iHexMaxHBytes;
          _hexBox.InternalSelect(pos, sel);
          _hexBox.ScrollByteIntoView(pos + sel);
        }
        else
        {
          sel -= _hexBox._iHexMaxHBytes;
          if (sel < 0)
          {
            pos = _bpiStart.Index;
            sel = -sel;
          }
          else
          {
            pos += _hexBox._iHexMaxHBytes;
            //sel -= _hexBox._iHexMaxHBytes;
          }

          _hexBox.InternalSelect(pos, sel);
          _hexBox.ScrollByteIntoView();
        }

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ShiftLeft(ref Message m)
      {
        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;

        if (pos + sel < 1)
          return true;

        if (pos + sel <= _bpiStart.Index)
        {
          if (pos == 0)
            return true;

          pos--;
          sel++;
        }
        else
        {
          sel = Math.Max(0, sel - 1);
        }

        _hexBox.ScrollByteIntoView();
        _hexBox.InternalSelect(pos, sel);

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ShiftRight(ref Message m)
      {
        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;

        if (pos + sel >= _hexBox._byteProvider.Length)
          return true;

        if (_bpiStart.Index <= pos)
        {
          sel++;
          _hexBox.InternalSelect(pos, sel);
          _hexBox.ScrollByteIntoView(pos + sel);
        }
        else
        {
          pos++;
          sel = Math.Max(0, sel - 1);
          _hexBox.InternalSelect(pos, sel);
          _hexBox.ScrollByteIntoView();
        }

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ShiftShiftKey(ref Message m)
      {
        if (_mouseDown)
          return true;
        if (_shiftDown)
          return true;

        _shiftDown = true;

        if (_hexBox._selectionLength > 0)
          return true;

        _bpiStart = new BytePositionInfo(_hexBox._bytePos, _hexBox._byteCharacterPos);

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ShiftTab(ref Message m)
      {
        if (_hexBox._keyInterpreter is StringKeyInterpreter)
        {
          _shiftDown = false;
          _hexBox.ActivateKeyInterpreter();
          _hexBox.ScrollByteIntoView();
          _hexBox.ReleaseSelection();
          _hexBox.UpdateCaret();
          _hexBox.Invalidate();
          return true;
        }

        if (_hexBox.Parent == null) return true;
        _hexBox.Parent.SelectNextControl(_hexBox, false, true, true, true);
        return true;
      }

      protected virtual bool PreProcessWmKeyDown_ShiftUp(ref Message m)
      {
        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;

        if (pos - _hexBox._iHexMaxHBytes < 0 && pos <= _bpiStart.Index)
          return true;

        if (_bpiStart.Index >= pos + sel)
        {
          pos = pos - _hexBox._iHexMaxHBytes;
          sel += _hexBox._iHexMaxHBytes;
          _hexBox.InternalSelect(pos, sel);
          _hexBox.ScrollByteIntoView();
        }
        else
        {
          sel -= _hexBox._iHexMaxHBytes;
          if (sel < 0)
          {
            pos = _bpiStart.Index + sel;
            sel = -sel;
            _hexBox.InternalSelect(pos, sel);
            _hexBox.ScrollByteIntoView();
          }
          else
          {
            sel -= _hexBox._iHexMaxHBytes;
            _hexBox.InternalSelect(pos, sel);
            _hexBox.ScrollByteIntoView(pos + sel);
          }
        }

        return true;
      }

      protected virtual bool PreProcessWmKeyDown_Tab(ref Message m)
      {
        if (_hexBox._stringViewVisible && _hexBox._keyInterpreter.GetType() == typeof(KeyInterpreter))
        {
          _hexBox.ActivateStringKeyInterpreter();
          _hexBox.ScrollByteIntoView();
          _hexBox.ReleaseSelection();
          _hexBox.UpdateCaret();
          _hexBox.Invalidate();
          return true;
        }

        if (_hexBox.Parent == null) return true;
        _hexBox.Parent.SelectNextControl(_hexBox, true, true, true, true);
        return true;
      }

      protected virtual bool PreProcessWmKeyDown_Up(ref Message m)
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (!(pos == 0 && cp == 0))
        {
          pos = Math.Max(-1, pos - _hexBox._iHexMaxHBytes);
          if (pos == -1)
            return true;

          _hexBox.SetPosition(pos);

          if (pos < _hexBox._startByte)
          {
            _hexBox.PerformScrollLineUp();
          }

          _hexBox.UpdateCaret();
          _hexBox.Invalidate();
        }

        _hexBox.ScrollByteIntoView();
        _hexBox.ReleaseSelection();

        return true;
      }

      protected bool RaiseKeyDown(Keys keyData)
      {
        KeyEventArgs e = new KeyEventArgs(keyData);
        _hexBox.OnKeyDown(e);
        return e.Handled;
      }

      #endregion PrePrcessWmKeyDown methods

      #region PreProcessWmChar methods

      public virtual bool PreProcessWmChar(ref Message m)
      {
        if (Control.ModifierKeys == Keys.Control)
        {
          return _hexBox.BasePreProcessMessage(ref m);
        }

        bool sw = _hexBox._byteProvider.SupportsWriteByte();
        bool si = _hexBox._byteProvider.SupportsInsertBytes();
        bool sd = _hexBox._byteProvider.SupportsDeleteBytes();

        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;
        int cp = _hexBox._byteCharacterPos;

        if (
          (!sw && pos != _hexBox._byteProvider.Length) ||
          (!si && pos == _hexBox._byteProvider.Length))
        {
          return _hexBox.BasePreProcessMessage(ref m);
        }

        char c = (char)m.WParam.ToInt32();

        if (Uri.IsHexDigit(c))
        {
          if (RaiseKeyPress(c))
            return true;

          if (_hexBox.ReadOnly)
            return true;

          bool isInsertMode = (pos == _hexBox._byteProvider.Length);

          // do insert when insertActive = true
          if (!isInsertMode && si && _hexBox.InsertActive && cp == 0)
            isInsertMode = true;

          if (sd && si && sel > 0)
          {
            _hexBox._byteProvider.DeleteBytes(pos, sel);
            isInsertMode = true;
            cp = 0;
            _hexBox.SetPosition(pos, cp);
          }

          _hexBox.ReleaseSelection();

          byte currentByte;
          if (isInsertMode)
            currentByte = 0;
          else
            currentByte = _hexBox._byteProvider.ReadByte(pos);

          string sCb = currentByte.ToString("X", System.Threading.Thread.CurrentThread.CurrentCulture);
          if (sCb.Length == 1)
            sCb = "0" + sCb;

          string sNewCb = c.ToString();
          if (cp == 0)
            sNewCb += sCb.Substring(1, 1);
          else
            sNewCb = sCb.Substring(0, 1) + sNewCb;
          byte newcb = byte.Parse(sNewCb, System.Globalization.NumberStyles.AllowHexSpecifier, System.Threading.Thread.CurrentThread.CurrentCulture);

          if (isInsertMode)
            _hexBox._byteProvider.InsertBytes(pos, new byte[] { newcb });
          else
            _hexBox._byteProvider.WriteByte(pos, newcb);

          PerformPosMoveRight();

          _hexBox.Invalidate();
          return true;
        }
        else
        {
          return _hexBox.BasePreProcessMessage(ref m);
        }
      }

      protected bool RaiseKeyPress(char keyChar)
      {
        KeyPressEventArgs e = new KeyPressEventArgs(keyChar);
        _hexBox.OnKeyPress(e);
        return e.Handled;
      }

      #endregion PreProcessWmChar methods

      #region PreProcessWmKeyUp methods

      public virtual bool PreProcessWmKeyUp(ref Message m)
      {
        System.Diagnostics.Debug.WriteLine("PreProcessWmKeyUp(ref Message m)", "KeyInterpreter");

        Keys vc = (Keys)m.WParam.ToInt32();

        Keys keyData = vc | Control.ModifierKeys;

        switch (keyData)
        {
          case Keys.ShiftKey:
          case Keys.Insert:
            if (RaiseKeyUp(keyData))
              return true;
            break;
        }

        switch (keyData)
        {
          case Keys.ShiftKey:
            _shiftDown = false;
            return true;

          case Keys.Insert:
            return PreProcessWmKeyUp_Insert(ref m);

          default:
            return _hexBox.BasePreProcessMessage(ref m);
        }
      }

      protected virtual bool PreProcessWmKeyUp_Insert(ref Message m)
      {
        _hexBox.InsertActive = !_hexBox.InsertActive;
        return true;
      }

      protected bool RaiseKeyUp(Keys keyData)
      {
        KeyEventArgs e = new KeyEventArgs(keyData);
        _hexBox.OnKeyUp(e);
        return e.Handled;
      }

      #endregion PreProcessWmKeyUp methods

      #region Misc

      private Dictionary<Keys, MessageDelegate> MessageHandlers
      {
        get
        {
          if (_messageHandlers == null)
          {
            _messageHandlers = new Dictionary<Keys, MessageDelegate>();
            _messageHandlers.Add(Keys.Left, new MessageDelegate(PreProcessWmKeyDown_Left)); // move left
            _messageHandlers.Add(Keys.Up, new MessageDelegate(PreProcessWmKeyDown_Up)); // move up
            _messageHandlers.Add(Keys.Right, new MessageDelegate(PreProcessWmKeyDown_Right)); // move right
            _messageHandlers.Add(Keys.Down, new MessageDelegate(PreProcessWmKeyDown_Down)); // move down
            _messageHandlers.Add(Keys.PageUp, new MessageDelegate(PreProcessWmKeyDown_PageUp)); // move pageup
            _messageHandlers.Add(Keys.PageDown, new MessageDelegate(PreProcessWmKeyDown_PageDown)); // move page down
            _messageHandlers.Add(Keys.Left | Keys.Shift, new MessageDelegate(PreProcessWmKeyDown_ShiftLeft)); // move left with selection
            _messageHandlers.Add(Keys.Up | Keys.Shift, new MessageDelegate(PreProcessWmKeyDown_ShiftUp)); // move up with selection
            _messageHandlers.Add(Keys.Right | Keys.Shift, new MessageDelegate(PreProcessWmKeyDown_ShiftRight)); // move right with selection
            _messageHandlers.Add(Keys.Down | Keys.Shift, new MessageDelegate(PreProcessWmKeyDown_ShiftDown)); // move down with selection
            _messageHandlers.Add(Keys.Tab, new MessageDelegate(PreProcessWmKeyDown_Tab)); // switch to string view
            _messageHandlers.Add(Keys.Back, new MessageDelegate(PreProcessWmKeyDown_Back)); // back
            _messageHandlers.Add(Keys.Delete, new MessageDelegate(PreProcessWmKeyDown_Delete)); // delete
            _messageHandlers.Add(Keys.Home, new MessageDelegate(PreProcessWmKeyDown_Home)); // move to home
            _messageHandlers.Add(Keys.End, new MessageDelegate(PreProcessWmKeyDown_End)); // move to end
            _messageHandlers.Add(Keys.ShiftKey | Keys.Shift, new MessageDelegate(PreProcessWmKeyDown_ShiftShiftKey)); // begin selection process
            _messageHandlers.Add(Keys.C | Keys.Control, new MessageDelegate(PreProcessWmKeyDown_ControlC)); // copy
            _messageHandlers.Add(Keys.X | Keys.Control, new MessageDelegate(PreProcessWmKeyDown_ControlX)); // cut
            _messageHandlers.Add(Keys.V | Keys.Control, new MessageDelegate(PreProcessWmKeyDown_ControlV)); // paste
          }
          return _messageHandlers;
        }
      }

      public virtual PointF GetCaretPointF(long byteIndex)
      {
        System.Diagnostics.Debug.WriteLine("GetCaretPointF()", "KeyInterpreter");

        return _hexBox.GetBytePointF(byteIndex);
      }

      protected virtual BytePositionInfo GetBytePositionInfo(Point p)
      {
        return _hexBox.GetHexBytePositionInfo(p);
      }

      protected virtual bool PerformPosMoveLeft()
      {
        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;
        int cp = _hexBox._byteCharacterPos;

        if (sel != 0)
        {
          cp = 0;
          _hexBox.SetPosition(pos, cp);
          _hexBox.ReleaseSelection();
        }
        else
        {
          if (pos == 0 && cp == 0)
            return true;

          if (cp > 0)
          {
            cp--;
          }
          else
          {
            pos = Math.Max(0, pos - 1);
            cp++;
          }

          _hexBox.SetPosition(pos, cp);

          if (pos < _hexBox._startByte)
          {
            _hexBox.PerformScrollLineUp();
          }
          _hexBox.UpdateCaret();
          _hexBox.Invalidate();
        }

        _hexBox.ScrollByteIntoView();
        return true;
      }

      protected virtual bool PerformPosMoveLeftByte()
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos == 0)
          return true;

        pos = Math.Max(0, pos - 1);
        cp = 0;

        _hexBox.SetPosition(pos, cp);

        if (pos < _hexBox._startByte)
        {
          _hexBox.PerformScrollLineUp();
        }
        _hexBox.UpdateCaret();
        _hexBox.ScrollByteIntoView();
        _hexBox.Invalidate();

        return true;
      }

      protected virtual bool PerformPosMoveRight()
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;
        long sel = _hexBox._selectionLength;

        if (sel != 0)
        {
          pos += sel;
          cp = 0;
          _hexBox.SetPosition(pos, cp);
          _hexBox.ReleaseSelection();
        }
        else
        {
          if (!(pos == _hexBox._byteProvider.Length && cp == 0))
          {
            if (cp > 0)
            {
              pos = Math.Min(_hexBox._byteProvider.Length, pos + 1);
              cp = 0;
            }
            else
            {
              cp++;
            }

            _hexBox.SetPosition(pos, cp);

            if (pos > _hexBox._endByte - 1)
            {
              _hexBox.PerformScrollLineDown();
            }
            _hexBox.UpdateCaret();
            _hexBox.Invalidate();
          }
        }

        _hexBox.ScrollByteIntoView();
        return true;
      }

      protected virtual bool PerformPosMoveRightByte()
      {
        long pos = _hexBox._bytePos;
        int cp = _hexBox._byteCharacterPos;

        if (pos == _hexBox._byteProvider.Length)
          return true;

        pos = Math.Min(_hexBox._byteProvider.Length, pos + 1);
        cp = 0;

        _hexBox.SetPosition(pos, cp);

        if (pos > _hexBox._endByte - 1)
        {
          _hexBox.PerformScrollLineDown();
        }
        _hexBox.UpdateCaret();
        _hexBox.ScrollByteIntoView();
        _hexBox.Invalidate();

        return true;
      }

      #endregion Misc
    }

    #endregion KeyInterpreter class

    #region StringKeyInterpreter class

    /// <summary>
    /// Handles user input such as mouse and keyboard input during string view edit
    /// </summary>
    private class StringKeyInterpreter : KeyInterpreter
    {
      #region Ctors

      public StringKeyInterpreter(HexBox hexBox)
        : base(hexBox)
      {
        _hexBox._byteCharacterPos = 0;
      }

      #endregion Ctors

      #region PreProcessWmKeyDown methods

      public override bool PreProcessWmKeyDown(ref Message m)
      {
        Keys vc = (Keys)m.WParam.ToInt32();

        Keys keyData = vc | Control.ModifierKeys;

        switch (keyData)
        {
          case Keys.Tab | Keys.Shift:
          case Keys.Tab:
            if (RaiseKeyDown(keyData))
              return true;
            break;
        }

        switch (keyData)
        {
          case Keys.Tab | Keys.Shift:
            return PreProcessWmKeyDown_ShiftTab(ref m);

          case Keys.Tab:
            return PreProcessWmKeyDown_Tab(ref m);

          default:
            return base.PreProcessWmKeyDown(ref m);
        }
      }

      protected override bool PreProcessWmKeyDown_Left(ref Message m)
      {
        return PerformPosMoveLeftByte();
      }

      protected override bool PreProcessWmKeyDown_Right(ref Message m)
      {
        return PerformPosMoveRightByte();
      }

      #endregion PreProcessWmKeyDown methods

      #region PreProcessWmChar methods

      public override bool PreProcessWmChar(ref Message m)
      {
        if (Control.ModifierKeys == Keys.Control)
        {
          return _hexBox.BasePreProcessMessage(ref m);
        }

        bool sw = _hexBox._byteProvider.SupportsWriteByte();
        bool si = _hexBox._byteProvider.SupportsInsertBytes();
        bool sd = _hexBox._byteProvider.SupportsDeleteBytes();

        long pos = _hexBox._bytePos;
        long sel = _hexBox._selectionLength;
        int cp = _hexBox._byteCharacterPos;

        if (
          (!sw && pos != _hexBox._byteProvider.Length) ||
          (!si && pos == _hexBox._byteProvider.Length))
        {
          return _hexBox.BasePreProcessMessage(ref m);
        }

        char c = (char)m.WParam.ToInt32();

        if (RaiseKeyPress(c))
          return true;

        if (_hexBox.ReadOnly)
          return true;

        bool isInsertMode = (pos == _hexBox._byteProvider.Length);

        // do insert when insertActive = true
        if (!isInsertMode && si && _hexBox.InsertActive)
          isInsertMode = true;

        if (sd && si && sel > 0)
        {
          _hexBox._byteProvider.DeleteBytes(pos, sel);
          isInsertMode = true;
          cp = 0;
          _hexBox.SetPosition(pos, cp);
        }

        _hexBox.ReleaseSelection();

        byte b = _hexBox.ByteCharConverter.ToByte(c);
        if (isInsertMode)
          _hexBox._byteProvider.InsertBytes(pos, new byte[] { b });
        else
          _hexBox._byteProvider.WriteByte(pos, b);

        PerformPosMoveRightByte();
        _hexBox.Invalidate();

        return true;
      }

      #endregion PreProcessWmChar methods

      #region Misc

      public override PointF GetCaretPointF(long byteIndex)
      {
        System.Diagnostics.Debug.WriteLine("GetCaretPointF()", "StringKeyInterpreter");

        Point gp = _hexBox.GetGridBytePoint(byteIndex);
        return _hexBox.GetByteStringPointF(gp);
      }

      protected override BytePositionInfo GetBytePositionInfo(Point p)
      {
        return _hexBox.GetStringBytePositionInfo(p);
      }

      #endregion Misc
    }

    #endregion StringKeyInterpreter class

    #region Fields

    /// <summary>Contains the ThumbTrack delay for scrolling in milliseconds.</summary>
    private const int THUMPTRACKDELAY = 50;

    /// <summary>Contains true, if the find (Find method) should be aborted.</summary>
    private bool _abortFind;

    /// <summary>Contains the current char position in one byte</summary>
    /// <example>
    ///   "1A"
    ///   "1" = char position of 0
    ///   "A" = char position of 1
    /// </example>
    private int _byteCharacterPos;

    /// <summary>Contains the current byte position.</summary>
    private long _bytePos = -1;

    /// <summary>Contains True if caret is visible.</summary>
    private bool _caretVisible;

    /// <summary>Contains an empty key interpreter without functionality.</summary>
    private EmptyKeyInterpreter _eki;

    /// <summary>Contains the index of the last visible byte.</summary>
    private long _endByte;

    /// <summary>Contains a value of the current finding position.</summary>
    private long _findingPos;

    /// <summary>Contains string format information for hex values.</summary>
    private string _hexStringFormat = "X";

    /// <summary>Contains the maximum of visible bytes.</summary>
    private int _iHexMaxBytes;

    /// <summary>Contains the maximum of visible horizontal bytes.</summary>
    private int _iHexMaxHBytes;

    /// <summary>Contains the maximum of visible vertical bytes.</summary>
    private int _iHexMaxVBytes;

    /// <summary>
    ///   Contains a state value about Insert or Write mode. When this value is true and
    ///   the ByteProvider SupportsInsert is true bytes are inserted instead of overridden.
    /// </summary>
    private bool _insertActive;

    /// <summary>Contains the current key interpreter.</summary>
    private IKeyInterpreter _keyInterpreter;

    /// <summary>Contains the default key interpreter.</summary>
    private KeyInterpreter _ki;

    /// <summary>Contains the Enviroment.TickCount of the last refresh.</summary>
    private int _lastThumbtrack;

    /// <summary>Contains the border bottom shift.</summary>
    private int _recBorderBottom = SystemInformation.Border3DSize.Height;

    /// <summary>Contains the border�s left shift.</summary>
    private int _recBorderLeft = SystemInformation.Border3DSize.Width;

    /// <summary>Contains the border�s right shift.</summary>
    private int _recBorderRight = SystemInformation.Border3DSize.Width;

    /// <summary>Contains the border�s top shift.</summary>
    private int _recBorderTop = SystemInformation.Border3DSize.Height;

    /// <summary>Contains the column info header rectangle bounds.</summary>
    private Rectangle _recColumnInfo;

    /// <summary>Contains the hole content bounds of all text.</summary>
    private Rectangle _recContent;

    /// <summary>Contains the hex data bounds.</summary>
    private Rectangle _recHex;

    /// <summary>Contains the line info bounds.</summary>
    private Rectangle _recLineInfo;

    /// <summary>Contains the string view bounds.</summary>
    private Rectangle _recStringView;

    /// <summary>Contains the scroll bars maximum value.</summary>
    private long _scrollVmax;

    /// <summary>Contains the scroll bars minimum value.</summary>
    private long _scrollVmin;

    /// <summary>Contains the scroll bars current position.</summary>
    private long _scrollVpos;

    /// <summary>Contains the string key interpreter.</summary>
    private StringKeyInterpreter _ski;

    /// <summary>Contains the index of the first visible byte.</summary>
    private long _startByte;

    /// <summary>Contains string format information for text drawing.</summary>
    private StringFormat _stringFormat;

    /// <summary>Contains the ThumbTrack scrolling position.</summary>
    private long _thumbTrackPosition;

    /// <summary>Contains a timer for ThumbTrack scrolling.</summary>
    private Timer _thumbTrackTimer = new Timer();

    /// <summary>Contains a vertical scroll.</summary>
    private VScrollBar _vScrollBar;

    #endregion Fields

    #region Events

    /// <summary>Occurs, when the value of BorderStyle property has changed.</summary>
    [Description("Occurs, when the value of BorderStyle property has changed.")]
    public event EventHandler BorderStyleChanged;

    /// <summary>Occurs, when the value of ByteProvider property has changed.</summary>
    [Description("Occurs, when the value of ByteProvider property has changed.")]
    public event EventHandler ByteProviderChanged;

    /// <summary>Occurs, when the value of BytesPerLine property has changed.</summary>
    [Description("Occurs, when the value of BytesPerLine property has changed.")]
    public event EventHandler BytesPerLineChanged;

    /// <summary>Occurs, when the CharSize property has changed.</summary>
    [Description("Occurs, when the CharSize property has changed")]
    public event EventHandler CharSizeChanged;

    /// <summary>Occurs, when the value of ColumnInfoVisibleChanged property has changed.</summary>
    [Description("Occurs, when the value of ColumnInfoVisibleChanged property has changed.")]
    public event EventHandler ColumnInfoVisibleChanged;

    /// <summary>Occurs, when Copy method was invoked and ClipBoardData changed.</summary>
    [Description("Occurs, when Copy method was invoked and ClipBoardData changed.")]
    public event EventHandler Copied;

    /// <summary>Occurs, when CopyHex method was invoked and ClipBoardData changed.</summary>
    [Description("Occurs, when CopyHex method was invoked and ClipBoardData changed.")]
    public event EventHandler CopiedHex;

    /// <summary>Occurs, when the value of CurrentLine property has changed.</summary>
    [Description("Occurs, when the value of CurrentLine property has changed.")]
    public event EventHandler CurrentLineChanged;

    /// <summary>Occurs, when the value of CurrentPositionInLine property has changed.</summary>
    [Description("Occurs, when the value of CurrentPositionInLine property has changed.")]
    public event EventHandler CurrentPositionInLineChanged;

    /// <summary>Occurs, when the value of GroupSeparatorVisibleChanged property has changed.</summary>
    [Description("Occurs, when the value of GroupSeparatorVisibleChanged property has changed.")]
    public event EventHandler GroupSeparatorVisibleChanged;

    /// <summary>Occurs, when the value of ColumnWidth property has changed.</summary>
    [Description("Occurs, when the value of GroupSize property has changed.")]
    public event EventHandler GroupSizeChanged;

    /// <summary>Occurs, when the value of HexCasing property has changed.</summary>
    [Description("Occurs, when the value of HexCasing property has changed.")]
    public event EventHandler HexCasingChanged;

    /// <summary>Occurs, when the value of HorizontalByteCount property has changed.</summary>
    [Description("Occurs, when the value of HorizontalByteCount property has changed.")]
    public event EventHandler HorizontalByteCountChanged;

    /// <summary>Occurs, when the value of InsertActive property has changed.</summary>
    [Description("Occurs, when the value of InsertActive property has changed.")]
    public event EventHandler InsertActiveChanged;

    /// <summary>Occurs, when the value of LineInfoVisible property has changed.</summary>
    [Description("Occurs, when the value of LineInfoVisible property has changed.")]
    public event EventHandler LineInfoVisibleChanged;

    /// <summary>Occurs, when the value of ReadOnly property has changed.</summary>
    [Description("Occurs, when the value of ReadOnly property has changed.")]
    public event EventHandler ReadOnlyChanged;

    /// <summary>Occurs, when the RequiredWidth property changes.</summary>
    [Description("Occurs, when the RequiredWidth property changes")]
    public event EventHandler RequiredWidthChanged;

    /// <summary>Occurs, when the value of SelectionLength property has changed.</summary>
    [Description("Occurs, when the value of SelectionLength property has changed.")]
    public event EventHandler SelectionLengthChanged;

    /// <summary>Occurs, when the value of SelectionStart property has changed.</summary>
    [Description("Occurs, when the value of SelectionStart property has changed.")]
    public event EventHandler SelectionStartChanged;

    /// <summary>Occurs, when the value of StringViewVisible property has changed.</summary>
    [Description("Occurs, when the value of StringViewVisible property has changed.")]
    public event EventHandler StringViewVisibleChanged;

    /// <summary>Occurs, when the value of UseFixedBytesPerLine property has changed.</summary>
    [Description("Occurs, when the value of UseFixedBytesPerLine property has changed.")]
    public event EventHandler UseFixedBytesPerLineChanged;

    /// <summary>Occurs, when the value of VerticalByteCount property has changed.</summary>
    [Description("Occurs, when the value of VerticalByteCount property has changed.")]
    public event EventHandler VerticalByteCountChanged;

    /// <summary>Occurs, when the value of VScrollBarVisible property has changed.</summary>
    [Description("Occurs, when the value of VScrollBarVisible property has changed.")]
    public event EventHandler VScrollBarVisibleChanged;

    #endregion Events

    #region Ctors

    /// <summary>Initializes a new instance of a HexBox class.</summary>
    public HexBox()
    {
      _vScrollBar = new VScrollBar();
      _vScrollBar.Scroll += new ScrollEventHandler(VerticalScrollBar_Scroll);

      _builtInContextMenu = new BuiltInContextMenu(this);

      BackColor = Color.White;
      Font = SystemFonts.MessageBoxFont;
      _stringFormat = new StringFormat(StringFormat.GenericTypographic);
      _stringFormat.FormatFlags = StringFormatFlags.MeasureTrailingSpaces;

      ActivateEmptyKeyInterpreter();

      SetStyle(ControlStyles.UserPaint, true);
      SetStyle(ControlStyles.DoubleBuffer, true);
      SetStyle(ControlStyles.AllPaintingInWmPaint, true);
      SetStyle(ControlStyles.ResizeRedraw, true);

      _thumbTrackTimer.Interval = 50;
      _thumbTrackTimer.Tick += new EventHandler(PerformScrollThumbTrack);
    }

    #endregion Ctors

    #region Scroll methods

    /// <summary>
    /// Scrolls the selection start byte into view
    /// </summary>
    public void ScrollByteIntoView()
    {
      System.Diagnostics.Debug.WriteLine("ScrollByteIntoView()", "HexBox");

      ScrollByteIntoView(_bytePos);
    }

    /// <summary>
    /// Scrolls the specific byte into view
    /// </summary>
    /// <param name="index">the index of the byte</param>
    public void ScrollByteIntoView(long index)
    {
      System.Diagnostics.Debug.WriteLine("ScrollByteIntoView(long index)", "HexBox");

      if (_byteProvider == null || _keyInterpreter == null)
        return;

      if (index < _startByte)
      {
        long line = (long)Math.Floor((double)index / (double)_iHexMaxHBytes);
        PerformScrollThumpPosition(line);
      }
      else if (index > _endByte)
      {
        long line = (long)Math.Floor((double)index / (double)_iHexMaxHBytes);
        line -= _iHexMaxVBytes - 1;
        PerformScrollThumpPosition(line);
      }
    }

    private void VerticalScrollBar_Scroll(object sender, ScrollEventArgs e)
    {
      switch (e.Type)
      {
        case ScrollEventType.Last:
          break;

        case ScrollEventType.EndScroll:
          break;

        case ScrollEventType.SmallIncrement:
          PerformScrollLineDown();
          break;

        case ScrollEventType.SmallDecrement:
          PerformScrollLineUp();
          break;

        case ScrollEventType.LargeIncrement:
          PerformScrollPageDown();
          break;

        case ScrollEventType.LargeDecrement:
          PerformScrollPageUp();
          break;

        case ScrollEventType.ThumbPosition:
          long lPos = FromScrollPos(e.NewValue);
          PerformScrollThumpPosition(lPos);
          break;

        case ScrollEventType.ThumbTrack:
          // to avoid performance problems use a refresh delay implemented with a timer
          if (_thumbTrackTimer.Enabled) // stop old timer
            _thumbTrackTimer.Enabled = false;

          // perform scroll immediately only if last refresh is very old
          int currentThumbTrack = System.Environment.TickCount;
          if (currentThumbTrack - _lastThumbtrack > THUMPTRACKDELAY)
          {
            PerformScrollThumbTrack(null, null);
            _lastThumbtrack = currentThumbTrack;
            break;
          }

          // start thumbtrack timer
          _thumbTrackPosition = FromScrollPos(e.NewValue);
          _thumbTrackTimer.Enabled = true;
          break;

        case ScrollEventType.First:
          break;

        default:
          break;
      }

      e.NewValue = ToScrollPos(_scrollVpos);
    }

    private long FromScrollPos(int value)
    {
      int max = 65535;
      if (_scrollVmax < max)
      {
        return (long)value;
      }
      else
      {
        double valperc = (double)value / (double)max * (double)100;
        long res = (int)Math.Floor((double)_scrollVmax / (double)100 * valperc);
        return res;
      }
    }

    private void PerformScrollLineDown() => PerformScrollLines(1);

    private void PerformScrollLines(int lines)
    {
      long pos;
      if (lines > 0)
        pos = Math.Min(_scrollVmax, _scrollVpos + lines);
      else if (lines < 0)
        pos = Math.Max(_scrollVmin, _scrollVpos + lines);
      else
        return;

      PerformScrollToLine(pos);
    }

    private void PerformScrollLineUp() => PerformScrollLines(-1);

    private void PerformScrollPageDown() => PerformScrollLines(_iHexMaxVBytes);

    private void PerformScrollPageUp() => PerformScrollLines(-_iHexMaxVBytes);

    /// <summary>Performs the thumb track scrolling after an delay.</summary>
    private void PerformScrollThumbTrack(object sender, EventArgs e)
    {
      _thumbTrackTimer.Enabled = false;
      PerformScrollThumpPosition(_thumbTrackPosition);
      _lastThumbtrack = Environment.TickCount;
    }

    private void PerformScrollThumpPosition(long pos)
    {
      // Bug fix: Scroll to end, do not scroll to end
      int difference = (_scrollVmax > 65535) ? 10 : 9;

      if (ToScrollPos(pos) == ToScrollMax(_scrollVmax) - difference)
        pos = _scrollVmax;
      // End Bug fix

      PerformScrollToLine(pos);
    }

    private void PerformScrollToLine(long pos)
    {
      if (pos < _scrollVmin || pos > _scrollVmax || pos == _scrollVpos)
        return;

      _scrollVpos = pos;

      UpdateVScroll();
      UpdateVisibilityBytes();
      UpdateCaret();
      Invalidate();
    }

    private int ToScrollMax(long value)
    {
      long max = 65535;
      return (value > max) ? (int)max : (int)value;
    }

    private int ToScrollPos(long value)
    {
      int max = 65535;

      if (_scrollVmax < max)
      {
        return (int)value;
      }
      else
      {
        double valperc = (double)value / (double)_scrollVmax * (double)100;
        int res = (int)Math.Floor((double)max / (double)100 * valperc);
        res = (int)Math.Max(_scrollVmin, res);
        res = (int)Math.Min(_scrollVmax, res);
        return res;
      }
    }

    private void UpdateScrollSize()
    {
      System.Diagnostics.Debug.WriteLine("UpdateScrollSize()", "HexBox");

      // Calc scroll bar info
      if (VScrollBarVisible && _byteProvider != null && _byteProvider.Length > 0 && _iHexMaxHBytes != 0)
      {
        long scrollmax = (long)Math.Ceiling((double)(_byteProvider.Length + 1) / (double)_iHexMaxHBytes - (double)_iHexMaxVBytes);
        scrollmax = Math.Max(0, scrollmax);

        long scrollpos = _startByte / _iHexMaxHBytes;

        if (scrollmax < _scrollVmax)
        {
          /* Data size has been decreased. */
          if (_scrollVpos == _scrollVmax)
            /* Scroll one line up if we at bottom. */
            PerformScrollLineUp();
        }

        if (scrollmax == _scrollVmax && scrollpos == _scrollVpos)
          return;

        _scrollVmin = 0;
        _scrollVmax = scrollmax;
        _scrollVpos = Math.Min(scrollpos, scrollmax);
        UpdateVScroll();
      }
      else if (VScrollBarVisible)
      {
        // Disable the scroll bar
        _scrollVmin = 0;
        _scrollVmax = 0;
        _scrollVpos = 0;
        UpdateVScroll();
      }
    }

    private void UpdateVScroll()
    {
      System.Diagnostics.Debug.WriteLine("UpdateVScroll()", "HexBox");

      int max = ToScrollMax(_scrollVmax);

      if (max > 0)
      {
        _vScrollBar.Minimum = 0;
        _vScrollBar.Maximum = max;
        _vScrollBar.Value = ToScrollPos(_scrollVpos);
        _vScrollBar.Visible = true;
      }
      else
      {
        _vScrollBar.Visible = false;
      }
    }

    #endregion Scroll methods

    #region Selection methods

    /// <summary>Returns true if Select method could be invoked.</summary>
    public bool CanSelectAll()
    {
      if (!Enabled)
        return false;

      if (_byteProvider == null)
        return false;

      return true;
    }

    /// <summary>Selects the hex box.</summary>
    /// <param name="start">The start index of the selection.</param>
    /// <param name="length">The length of the selection.</param>
    public void Select(long start, long length)
    {
      if (ByteProvider == null)
        return;

      if (!Enabled)
        return;

      InternalSelect(start, length);
      ScrollByteIntoView();
    }

    /// <summary>Selects all bytes.</summary>
    public void SelectAll()
    {
      if (ByteProvider == null)
        return;

      Select(0, ByteProvider.Length);
    }

    private void InternalSelect(long start, long length)
    {
      long pos = start;
      long sel = length;
      int cp = 0;

      if (sel > 0 && _caretVisible)
        DestroyCaret();
      else if (sel == 0 && !_caretVisible)
        CreateCaret();

      SetPosition(pos, cp);
      SetSelectionLength(sel);

      UpdateCaret();
      Invalidate();
    }

    private void ReleaseSelection()
    {
      System.Diagnostics.Debug.WriteLine("ReleaseSelection()", "HexBox");

      if (_selectionLength == 0)
        return;

      _selectionLength = 0;
      OnSelectionLengthChanged(EventArgs.Empty);

      if (!_caretVisible)
        CreateCaret();
      else
        UpdateCaret();

      Invalidate();
    }

    #endregion Selection methods

    #region Key interpreter methods

    private void ActivateEmptyKeyInterpreter()
    {
      if (_eki == null)
        _eki = new EmptyKeyInterpreter(this);

      if (_eki == _keyInterpreter)
        return;

      if (_keyInterpreter != null)
        _keyInterpreter.Deactivate();

      _keyInterpreter = _eki;
      _keyInterpreter.Activate();
    }

    private void ActivateKeyInterpreter()
    {
      if (_ki == null)
        _ki = new KeyInterpreter(this);

      if (_ki == _keyInterpreter)
        return;

      if (_keyInterpreter != null)
        _keyInterpreter.Deactivate();

      _keyInterpreter = _ki;
      _keyInterpreter.Activate();
    }

    private void ActivateStringKeyInterpreter()
    {
      if (_ski == null)
        _ski = new StringKeyInterpreter(this);

      if (_ski == _keyInterpreter)
        return;

      if (_keyInterpreter != null)
        _keyInterpreter.Deactivate();

      _keyInterpreter = _ski;
      _keyInterpreter.Activate();
    }

    #endregion Key interpreter methods

    #region Caret methods

    private void CreateCaret()
    {
      if (_byteProvider == null || _keyInterpreter == null || _caretVisible || !this.Focused)
        return;

      System.Diagnostics.Debug.WriteLine("CreateCaret()", "HexBox");

      // define the caret width depending on InsertActive mode
      int caretWidth = (this.InsertActive) ? 1 : (int)_charSize.Width;
      int caretHeight = (int)_charSize.Height;
      NativeMethods.CreateCaret(Handle, IntPtr.Zero, caretWidth, caretHeight);

      UpdateCaret();

      NativeMethods.ShowCaret(Handle);

      _caretVisible = true;
    }

    private void DestroyCaret()
    {
      if (!_caretVisible)
        return;

      System.Diagnostics.Debug.WriteLine("DestroyCaret()", "HexBox");

      NativeMethods.DestroyCaret();
      _caretVisible = false;
    }

    private BytePositionInfo GetHexBytePositionInfo(Point p)
    {
      System.Diagnostics.Debug.WriteLine("GetHexBytePositionInfo()", "HexBox");

      long bytePos;
      int byteCharaterPos;

      float x = ((float)(p.X - _recHex.X) / _charSize.Width);
      float y = ((float)(p.Y - _recHex.Y) / _charSize.Height);
      int iX = (int)x;
      int iY = (int)y;

      int hPos = (iX / 3 + 1);

      bytePos = Math.Min(_byteProvider.Length,
        _startByte + (_iHexMaxHBytes * (iY + 1) - _iHexMaxHBytes) + hPos - 1);
      byteCharaterPos = (iX % 3);
      if (byteCharaterPos > 1)
        byteCharaterPos = 1;

      if (bytePos == _byteProvider.Length)
        byteCharaterPos = 0;

      if (bytePos < 0)
        return new BytePositionInfo(0, 0);

      return new BytePositionInfo(bytePos, byteCharaterPos);
    }

    private BytePositionInfo GetStringBytePositionInfo(Point p)
    {
      System.Diagnostics.Debug.WriteLine("GetStringBytePositionInfo()", "HexBox");

      long bytePos;
      int byteCharacterPos;

      float x = ((float)(p.X - _recStringView.X) / _charSize.Width);
      float y = ((float)(p.Y - _recStringView.Y) / _charSize.Height);
      int iX = (int)x;
      int iY = (int)y;

      int hPos = iX + 1;

      bytePos = Math.Min(_byteProvider.Length,
        _startByte + (_iHexMaxHBytes * (iY + 1) - _iHexMaxHBytes) + hPos - 1);
      byteCharacterPos = 0;

      if (bytePos < 0)
        return new BytePositionInfo(0, 0);

      return new BytePositionInfo(bytePos, byteCharacterPos);
    }

    private void SetCaretPosition(Point p)
    {
      System.Diagnostics.Debug.WriteLine("SetCaretPosition()", "HexBox");

      if (_byteProvider == null || _keyInterpreter == null)
        return;

      long pos = _bytePos;
      int cp = _byteCharacterPos;

      if (_recHex.Contains(p))
      {
        BytePositionInfo bpi = GetHexBytePositionInfo(p);
        pos = bpi.Index;
        cp = bpi.CharacterPosition;

        SetPosition(pos, cp);

        ActivateKeyInterpreter();
        UpdateCaret();
        Invalidate();
      }
      else if (_recStringView.Contains(p))
      {
        BytePositionInfo bpi = GetStringBytePositionInfo(p);
        pos = bpi.Index;
        cp = bpi.CharacterPosition;

        SetPosition(pos, cp);

        ActivateStringKeyInterpreter();
        UpdateCaret();
        Invalidate();
      }
    }

    private void UpdateCaret()
    {
      if (_byteProvider == null || _keyInterpreter == null)
        return;

      System.Diagnostics.Debug.WriteLine("UpdateCaret()", "HexBox");

      long byteIndex = _bytePos - _startByte;
      PointF p = _keyInterpreter.GetCaretPointF(byteIndex);
      p.X += _byteCharacterPos * _charSize.Width;
      NativeMethods.SetCaretPos((int)p.X, (int)p.Y);
    }

    #endregion Caret methods

    #region PreProcessMessage methods

    /// <summary>Preprocessor windows messages.</summary>
    /// <param name="m">The message to process.</param>
    /// <returns>Return True, if the message was processed</returns>
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true), SecurityPermission(SecurityAction.InheritanceDemand, UnmanagedCode = true)]
    public override bool PreProcessMessage(ref Message m)
    {
      return m.Msg switch
      {
        NativeMethods.WM_KEYDOWN => _keyInterpreter.PreProcessWmKeyDown(ref m),
        NativeMethods.WM_CHAR => _keyInterpreter.PreProcessWmChar(ref m),
        NativeMethods.WM_KEYUP => _keyInterpreter.PreProcessWmKeyUp(ref m),
        _ => base.PreProcessMessage(ref m),
      };
    }

    private bool BasePreProcessMessage(ref Message m) => base.PreProcessMessage(ref m);

    #endregion PreProcessMessage methods

    #region Find methods

    /// <summary>
    /// Gets a value that indicates the current position during Find method execution.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long CurrentFindingPosition => _findingPos;

    /// <summary>Aborts a working Find method.</summary>
    public void AbortFind() => _abortFind = true;

    /// <summary>Searches the current ByteProvider.</summary>
    /// <param name="options">contains all find options</param>
    /// <returns>
    ///   The SelectionStart property value if find was successful or
    ///   -1 if there is no match
    ///   -2 if Find was aborted.
    /// </returns>
    public long Find(FindOptions options)
    {
      var startIndex = this.SelectionStart + this.SelectionLength;
      int match = 0;

      byte[] buffer1 = null;
      byte[] buffer2 = null;
      if (options.Type == FindType.Text && options.MatchCase)
      {
        if (options.FindBuffer == null || options.FindBuffer.Length == 0)
          throw new ArgumentException("FindBuffer can not be null when Type: Text and MatchCase: false");
        buffer1 = options.FindBuffer;
      }
      else if (options.Type == FindType.Text && !options.MatchCase)
      {
        if (options.FindBufferLowerCase == null || options.FindBufferLowerCase.Length == 0)
          throw new ArgumentException("FindBufferLowerCase can not be null when Type is Text and MatchCase is true");

        if (options.FindBufferUpperCase == null || options.FindBufferUpperCase.Length == 0)
          throw new ArgumentException("FindBufferUpperCase can not be null when Type is Text and MatchCase is true");

        if (options.FindBufferLowerCase.Length != options.FindBufferUpperCase.Length)
          throw new ArgumentException("FindBufferUpperCase and FindBufferUpperCase must have the same size when Type is Text and MatchCase is true");

        buffer1 = options.FindBufferLowerCase;
        buffer2 = options.FindBufferUpperCase;
      }
      else if (options.Type == FindType.Hex)
      {
        if (options.Hex == null || options.Hex.Length == 0)
          throw new ArgumentException("Hex can not be null when Type is Hex");
        buffer1 = options.Hex;
      }

      int buffer1Length = buffer1.Length;

      _abortFind = false;

      for (long pos = startIndex; pos < _byteProvider.Length; pos++)
      {
        if (_abortFind)
          return -2;

        if (pos % 1000 == 0) // for performance reasons: DoEvents only 1 times per 1000 loops
          Application.DoEvents();

        byte compareByte = _byteProvider.ReadByte(pos);
        bool buffer1Match = compareByte == buffer1[match];
        bool hasBuffer2 = buffer2 != null;
        bool buffer2Match = hasBuffer2 ? compareByte == buffer2[match] : false;
        bool isMatch = buffer1Match || buffer2Match;

        if (!isMatch)
        {
          pos -= match;
          match = 0;
          _findingPos = pos;
          continue;
        }

        match++;

        if (match == buffer1Length)
        {
          long bytePos = pos - buffer1Length + 1;
          Select(bytePos, buffer1Length);
          ScrollByteIntoView(_bytePos + _selectionLength);
          ScrollByteIntoView(_bytePos);

          return bytePos;
        }
      }

      return -1;
    }

    #endregion Find methods

    #region Copy, Cut and Paste methods

    /// <summary>
    /// Return true if Copy method could be invoked.
    /// </summary>
    public bool CanCopy()
    {
      if (_selectionLength < 1 || _byteProvider == null)
        return false;

      return true;
    }

    /// <summary>
    /// Return true if Cut method could be invoked.
    /// </summary>
    public bool CanCut()
    {
      if (ReadOnly || !this.Enabled)
        return false;
      if (_byteProvider == null)
        return false;
      if (_selectionLength < 1 || !_byteProvider.SupportsDeleteBytes())
        return false;

      return true;
    }

    /// <summary>
    /// Return true if Paste method could be invoked.
    /// </summary>
    public bool CanPaste()
    {
      if (ReadOnly || !this.Enabled) return false;

      if (_byteProvider == null || !_byteProvider.SupportsInsertBytes())
        return false;

      if (!_byteProvider.SupportsDeleteBytes() && _selectionLength > 0)
        return false;

      IDataObject da = Clipboard.GetDataObject();
      if (da.GetDataPresent("BinaryData"))
        return true;
      else if (da.GetDataPresent(typeof(string)))
        return true;
      else
        return false;
    }

    /// <summary>Return true if PasteHex method could be invoked.</summary>
    public bool CanPasteHex()
    {
      if (!CanPaste()) return false;

      byte[] buffer = null;
      IDataObject da = Clipboard.GetDataObject();
      if (da.GetDataPresent(typeof(string)))
      {
        string hexString = (string)da.GetData(typeof(string));
        buffer = ConvertHexToBytes(hexString);
        return (buffer != null);
      }

      return false;
    }

    /// <summary>
    /// Copies the current selection in the hex box to the Clipboard.
    /// </summary>
    public void Copy()
    {
      if (!CanCopy()) return;

      // put bytes into buffer
      byte[] buffer = GetCopyData();

      DataObject da = new DataObject();

      // set string buffer clipbard data
      string sBuffer = System.Text.Encoding.ASCII.GetString(buffer, 0, buffer.Length);
      da.SetData(typeof(string), sBuffer);

      //set memorystream (BinaryData) clipboard data
      System.IO.MemoryStream ms = new System.IO.MemoryStream(buffer, 0, buffer.Length, false, true);
      da.SetData("BinaryData", ms);

      Clipboard.SetDataObject(da, true);
      UpdateCaret();
      ScrollByteIntoView();
      Invalidate();

      OnCopied(EventArgs.Empty);
    }

    /// <summary>Copies the current selection in the hex box to the Clipboard in hex format.</summary>
    public void CopyHex()
    {
      if (!CanCopy())
        return;

      // put bytes into buffer
      byte[] buffer = GetCopyData();

      DataObject da = new DataObject();

      // set string buffer clipbard data
      string hexString = ConvertBytesToHex(buffer); ;
      da.SetData(typeof(string), hexString);

      //set memorystream (BinaryData) clipboard data
      System.IO.MemoryStream ms = new System.IO.MemoryStream(buffer, 0, buffer.Length, false, true);
      da.SetData("BinaryData", ms);

      Clipboard.SetDataObject(da, true);
      UpdateCaret();
      ScrollByteIntoView();
      Invalidate();

      OnCopiedHex(EventArgs.Empty);
    }

    /// <summary>Moves the current selection in the hex box to the Clipboard.</summary>
    public void Cut()
    {
      if (!CanCut())
        return;

      Copy();

      _byteProvider.DeleteBytes(_bytePos, _selectionLength);
      _byteCharacterPos = 0;
      UpdateCaret();
      ScrollByteIntoView();
      ReleaseSelection();
      Invalidate();
      Refresh();
    }

    /// <summary>Replaces the current selection in the hex box with the contents of the Clipboard.</summary>
    public void Paste()
    {
      if (!CanPaste())
        return;

      if (_selectionLength > 0)
        _byteProvider.DeleteBytes(_bytePos, _selectionLength);

      byte[] buffer = null;
      IDataObject da = Clipboard.GetDataObject();

      if (da.GetDataPresent("BinaryData"))
      {
        System.IO.MemoryStream ms = (System.IO.MemoryStream)da.GetData("BinaryData");
        buffer = new byte[ms.Length];
        ms.Read(buffer, 0, buffer.Length);
      }
      else if (da.GetDataPresent(typeof(string)))
      {
        string sBuffer = (string)da.GetData(typeof(string));
        buffer = System.Text.Encoding.ASCII.GetBytes(sBuffer);
      }
      else
      {
        return;
      }

      _byteProvider.InsertBytes(_bytePos, buffer);

      SetPosition(_bytePos + buffer.Length, 0);

      ReleaseSelection();
      ScrollByteIntoView();
      UpdateCaret();
      Invalidate();
    }

    /// <summary>Replaces the current selection in the hex box with the hex string data of the Clipboard.</summary>
    public void PasteHex()
    {
      if (!CanPaste())
        return;

      byte[] buffer = null;
      IDataObject da = Clipboard.GetDataObject();
      if (da.GetDataPresent(typeof(string)))
      {
        string hexString = (string)da.GetData(typeof(string));
        buffer = ConvertHexToBytes(hexString);
        if (buffer == null)
          return;
      }
      else
      {
        return;
      }

      if (_selectionLength > 0)
        _byteProvider.DeleteBytes(_bytePos, _selectionLength);

      _byteProvider.InsertBytes(_bytePos, buffer);

      SetPosition(_bytePos + buffer.Length, 0);

      ReleaseSelection();
      ScrollByteIntoView();
      UpdateCaret();
      Invalidate();
    }

    private byte[] GetCopyData()
    {
      if (!CanCopy())
        return new byte[0];

      // put bytes into buffer
      byte[] buffer = new byte[_selectionLength];
      int id = -1;

      for (long i = _bytePos; i < _bytePos + _selectionLength; i++)
      {
        id++;

        buffer[id] = _byteProvider.ReadByte(i);
      }

      return buffer;
    }

    #endregion Copy, Cut and Paste methods

    #region Paint methods

    /// <summary>Paints the hex box.</summary>
    /// <param name="e">A PaintEventArgs that contains the event data.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
      base.OnPaint(e);

      if (_byteProvider == null)
        return;

      System.Diagnostics.Debug.WriteLine("OnPaint " + DateTime.Now.ToString(), "HexBox");

      // draw only in the content rectangle, so exclude the border and the scrollbar.
      Region r = new Region(ClientRectangle);
      r.Exclude(_recContent);
      e.Graphics.ExcludeClip(r);

      UpdateVisibilityBytes();

      if (_lineInfoVisible)
        PaintLineInfo(e.Graphics, _startByte, _endByte);

      if (!_stringViewVisible)
      {
        PaintHex(e.Graphics, _startByte, _endByte);
      }
      else
      {
        PaintHexAndStringView(e.Graphics, _startByte, _endByte);
        if (_shadowSelectionVisible)
          PaintCurrentBytesSign(e.Graphics);
      }

      if (_columnInfoVisible)
        PaintHeaderRow(e.Graphics);

      if (_groupSeparatorVisible)
        PaintColumnSeparator(e.Graphics);
    }

    /// <summary>Paints the background.</summary>
    /// <param name="e">A PaintEventArgs that contains the event data.</param>
    protected override void OnPaintBackground(PaintEventArgs e)
    {
      switch (_borderStyle)
      {
        case BorderStyle.Fixed3D:
        {
          if (TextBoxRenderer.IsSupported)
          {
            VisualStyleElement state = VisualStyleElement.TextBox.TextEdit.Normal;
            Color backColor = this.BackColor;

            if (this.Enabled)
            {
              if (this.ReadOnly)
                state = VisualStyleElement.TextBox.TextEdit.ReadOnly;
              else if (this.Focused)
                state = VisualStyleElement.TextBox.TextEdit.Focused;
            }
            else
            {
              state = VisualStyleElement.TextBox.TextEdit.Disabled;
              backColor = this.BackColorDisabled;
            }

            VisualStyleRenderer vsr = new VisualStyleRenderer(state);
            vsr.DrawBackground(e.Graphics, this.ClientRectangle);

            Rectangle rectContent = vsr.GetBackgroundContentRectangle(e.Graphics, this.ClientRectangle);
            e.Graphics.FillRectangle(new SolidBrush(backColor), rectContent);
          }
          else
          {
            // draw background
            e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);

            // draw default border
            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Sunken);
          }

          break;
        }
        case BorderStyle.FixedSingle:
        {
          // draw background
          e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);

          // draw fixed single border
          ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.Black, ButtonBorderStyle.Solid);
          break;
        }
        default:
        {
          // draw background
          e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);
          break;
        }
      }
    }

    private Color GetDefaultForeColor() => Enabled ? ForeColor : Color.Gray;

    private void PaintColumnInfo(Graphics g, byte b, Brush brush, int col)
    {
      PointF headerPointF = GetColumnInfoPointF(col);

      string sB = ConvertByteToHex(b);

      g.DrawString(sB.Substring(0, 1), Font, brush, headerPointF, _stringFormat);
      headerPointF.X += _charSize.Width;
      g.DrawString(sB.Substring(1, 1), Font, brush, headerPointF, _stringFormat);
    }

    private void PaintColumnSeparator(Graphics g)
    {
      for (int col = GroupSize; col < _iHexMaxHBytes; col += GroupSize)
      {
        var pen = new Pen(new SolidBrush(this.InfoForeColor), 1);
        PointF headerPointF = GetColumnInfoPointF(col);
        headerPointF.X -= _charSize.Width / 2;
        g.DrawLine(pen, headerPointF, new PointF(headerPointF.X, headerPointF.Y + _recColumnInfo.Height + _recHex.Height));
        if (StringViewVisible)
        {
          PointF byteStringPointF = GetByteStringPointF(new Point(col, 0));
          headerPointF.X -= 2;
          g.DrawLine(pen, new PointF(byteStringPointF.X, byteStringPointF.Y), new PointF(byteStringPointF.X, byteStringPointF.Y + _recHex.Height));
        }
      }
    }

    private void PaintCurrentByteSign(Graphics g, Rectangle rec)
    {
      // Stack overflow exception on big files - workaround
      if (rec.Top < 0 || rec.Left < 0 || rec.Width <= 0 || rec.Height <= 0)
        return;

      Bitmap myBitmap = new Bitmap(rec.Width, rec.Height);
      Graphics bitmapGraphics = Graphics.FromImage(myBitmap);

      SolidBrush greenBrush = new SolidBrush(_shadowSelectionColor);

      bitmapGraphics.FillRectangle(greenBrush, 0, 0, rec.Width, rec.Height);

      g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;
      g.DrawImage(myBitmap, rec.Left, rec.Top);
    }

    private void PaintCurrentBytesSign(Graphics g)
    {
      if (_keyInterpreter != null && _bytePos != -1 && Enabled)
      {
        if (_keyInterpreter.GetType() == typeof(KeyInterpreter))
        {
          if (_selectionLength == 0)
          {
            Point gp = GetGridBytePoint(_bytePos - _startByte);
            PointF pf = GetByteStringPointF(gp);
            Size s = new Size((int)_charSize.Width, (int)_charSize.Height);
            Rectangle r = new Rectangle((int)pf.X, (int)pf.Y, s.Width, s.Height);
            if (r.IntersectsWith(_recStringView))
            {
              r.Intersect(_recStringView);
              PaintCurrentByteSign(g, r);
            }
          }
          else
          {
            int lineWidth = (int)(_recStringView.Width - _charSize.Width);

            Point startSelGridPoint = GetGridBytePoint(_bytePos - _startByte);
            PointF startSelPointF = GetByteStringPointF(startSelGridPoint);

            Point endSelGridPoint = GetGridBytePoint(_bytePos - _startByte + _selectionLength - 1);
            PointF endSelPointF = GetByteStringPointF(endSelGridPoint);

            int multiLine = endSelGridPoint.Y - startSelGridPoint.Y;
            if (multiLine == 0)
            {
              Rectangle singleLine = new Rectangle(
                (int)startSelPointF.X,
                (int)startSelPointF.Y,
                (int)(endSelPointF.X - startSelPointF.X + _charSize.Width),
                (int)_charSize.Height);
              if (singleLine.IntersectsWith(_recStringView))
              {
                singleLine.Intersect(_recStringView);
                PaintCurrentByteSign(g, singleLine);
              }
            }
            else
            {
              Rectangle firstLine = new Rectangle(
                (int)startSelPointF.X,
                (int)startSelPointF.Y,
                (int)(_recStringView.X + lineWidth - startSelPointF.X + _charSize.Width),
                (int)_charSize.Height);
              if (firstLine.IntersectsWith(_recStringView))
              {
                firstLine.Intersect(_recStringView);
                PaintCurrentByteSign(g, firstLine);
              }

              if (multiLine > 1)
              {
                Rectangle betweenLines = new Rectangle(
                  _recStringView.X,
                  (int)(startSelPointF.Y + _charSize.Height),
                  (int)(_recStringView.Width),
                  (int)(_charSize.Height * (multiLine - 1)));
                if (betweenLines.IntersectsWith(_recStringView))
                {
                  betweenLines.Intersect(_recStringView);
                  PaintCurrentByteSign(g, betweenLines);
                }
              }

              Rectangle lastLine = new Rectangle(
                _recStringView.X,
                (int)endSelPointF.Y,
                (int)(endSelPointF.X - _recStringView.X + _charSize.Width),
                (int)_charSize.Height);
              if (lastLine.IntersectsWith(_recStringView))
              {
                lastLine.Intersect(_recStringView);
                PaintCurrentByteSign(g, lastLine);
              }
            }
          }
        }
        else
        {
          if (_selectionLength == 0)
          {
            Point gp = GetGridBytePoint(_bytePos - _startByte);
            PointF pf = GetBytePointF(gp);
            Size s = new Size((int)_charSize.Width * 2, (int)_charSize.Height);
            Rectangle r = new Rectangle((int)pf.X, (int)pf.Y, s.Width, s.Height);
            PaintCurrentByteSign(g, r);
          }
          else
          {
            int lineWidth = (int)(_recHex.Width - _charSize.Width * 5);

            Point startSelGridPoint = GetGridBytePoint(_bytePos - _startByte);
            PointF startSelPointF = GetBytePointF(startSelGridPoint);

            Point endSelGridPoint = GetGridBytePoint(_bytePos - _startByte + _selectionLength - 1);
            PointF endSelPointF = GetBytePointF(endSelGridPoint);

            int multiLine = endSelGridPoint.Y - startSelGridPoint.Y;
            if (multiLine == 0)
            {
              Rectangle singleLine = new Rectangle(
                (int)startSelPointF.X,
                (int)startSelPointF.Y,
                (int)(endSelPointF.X - startSelPointF.X + _charSize.Width * 2),
                (int)_charSize.Height);

              if (singleLine.IntersectsWith(_recHex))
              {
                singleLine.Intersect(_recHex);
                PaintCurrentByteSign(g, singleLine);
              }
            }
            else
            {
              Rectangle firstLine = new Rectangle(
                (int)startSelPointF.X,
                (int)startSelPointF.Y,
                (int)(_recHex.X + lineWidth - startSelPointF.X + _charSize.Width * 2),
                (int)_charSize.Height);

              if (firstLine.IntersectsWith(_recHex))
              {
                firstLine.Intersect(_recHex);
                PaintCurrentByteSign(g, firstLine);
              }

              if (multiLine > 1)
              {
                Rectangle betweenLines = new Rectangle(
                  _recHex.X,
                  (int)(startSelPointF.Y + _charSize.Height),
                  (int)(lineWidth + _charSize.Width * 2),
                  (int)(_charSize.Height * (multiLine - 1)));

                if (betweenLines.IntersectsWith(_recHex))
                {
                  betweenLines.Intersect(_recHex);
                  PaintCurrentByteSign(g, betweenLines);
                }
              }

              Rectangle lastLine = new Rectangle(
                _recHex.X,
                (int)endSelPointF.Y,
                (int)(endSelPointF.X - _recHex.X + _charSize.Width * 2),
                (int)_charSize.Height);

              if (lastLine.IntersectsWith(_recHex))
              {
                lastLine.Intersect(_recHex);
                PaintCurrentByteSign(g, lastLine);
              }
            }
          }
        }
      }
    }

    private void PaintHeaderRow(Graphics g)
    {
      Brush brush = new SolidBrush(this.InfoForeColor);
      for (int col = 0; col < _iHexMaxHBytes; col++)
      {
        PaintColumnInfo(g, (byte)col, brush, col);
      }
    }

    private void PaintHex(Graphics g, long startByte, long endByte)
    {
      Brush brush = new SolidBrush(GetDefaultForeColor());
      Brush selBrush = new SolidBrush(_selectionForeColor);
      Brush selBrushBack = new SolidBrush(_selectionBackColor);

      int counter = -1;
      long intern_endByte = Math.Min(_byteProvider.Length - 1, endByte + _iHexMaxHBytes);

      bool isKeyInterpreterActive = _keyInterpreter == null || _keyInterpreter.GetType() == typeof(KeyInterpreter);

      for (long i = startByte; i < intern_endByte + 1; i++)
      {
        counter++;
        Point gridPoint = GetGridBytePoint(counter);
        byte b = _byteProvider.ReadByte(i);

        bool isSelectedByte = i >= _bytePos && i <= (_bytePos + _selectionLength - 1) && _selectionLength != 0;

        if (isSelectedByte && isKeyInterpreterActive)
        {
          PaintHexStringSelected(g, b, selBrush, selBrushBack, gridPoint);
        }
        else
        {
          PaintHexString(g, b, brush, gridPoint);
        }
      }
    }

    private void PaintHexAndStringView(Graphics g, long startByte, long endByte)
    {
      Brush brush = new SolidBrush(GetDefaultForeColor());
      Brush selBrush = new SolidBrush(_selectionForeColor);
      Brush selBrushBack = new SolidBrush(_selectionBackColor);

      int counter = -1;
      long intern_endByte = Math.Min(_byteProvider.Length - 1, endByte + _iHexMaxHBytes);

      bool isKeyInterpreterActive = _keyInterpreter == null || _keyInterpreter.GetType() == typeof(KeyInterpreter);
      bool isStringKeyInterpreterActive = _keyInterpreter != null && _keyInterpreter.GetType() == typeof(StringKeyInterpreter);

      for (long i = startByte; i < intern_endByte + 1; i++)
      {
        counter++;
        Point gridPoint = GetGridBytePoint(counter);
        PointF byteStringPointF = GetByteStringPointF(gridPoint);
        byte b = _byteProvider.ReadByte(i);

        bool isSelectedByte = i >= _bytePos && i <= (_bytePos + _selectionLength - 1) && _selectionLength != 0;

        if (isSelectedByte && isKeyInterpreterActive)
          PaintHexStringSelected(g, b, selBrush, selBrushBack, gridPoint);
        else
          PaintHexString(g, b, brush, gridPoint);

        string s = new String(ByteCharConverter.ToChar(b), 1);

        if (isSelectedByte && isStringKeyInterpreterActive)
        {
          g.FillRectangle(selBrushBack, byteStringPointF.X, byteStringPointF.Y, _charSize.Width, _charSize.Height);
          g.DrawString(s, Font, selBrush, byteStringPointF, _stringFormat);
        }
        else
        {
          g.DrawString(s, Font, brush, byteStringPointF, _stringFormat);
        }
      }
    }

    private void PaintHexString(Graphics g, byte b, Brush brush, Point gridPoint)
    {
      PointF bytePointF = GetBytePointF(gridPoint);

      string sB = ConvertByteToHex(b);

      g.DrawString(sB.Substring(0, 1), Font, brush, bytePointF, _stringFormat);
      bytePointF.X += _charSize.Width;
      g.DrawString(sB.Substring(1, 1), Font, brush, bytePointF, _stringFormat);
    }

    private void PaintHexStringSelected(Graphics g, byte b, Brush brush, Brush brushBack, Point gridPoint)
    {
      string sB = b.ToString(_hexStringFormat, System.Threading.Thread.CurrentThread.CurrentCulture);
      if (sB.Length == 1)
        sB = "0" + sB;

      PointF bytePointF = GetBytePointF(gridPoint);

      bool isLastLineChar = (gridPoint.X + 1 == _iHexMaxHBytes);
      float bcWidth = (isLastLineChar) ? _charSize.Width * 2 : _charSize.Width * 3;

      g.FillRectangle(brushBack, bytePointF.X, bytePointF.Y, bcWidth, _charSize.Height);
      g.DrawString(sB.Substring(0, 1), Font, brush, bytePointF, _stringFormat);
      bytePointF.X += _charSize.Width;
      g.DrawString(sB.Substring(1, 1), Font, brush, bytePointF, _stringFormat);
    }

    private void PaintLineInfo(Graphics g, long startByte, long endByte)
    {
      // Ensure endByte isn't > length of array.
      endByte = Math.Min(_byteProvider.Length - 1, endByte);

      Color lineInfoColor = (this.InfoForeColor != Color.Empty) ? this.InfoForeColor : this.ForeColor;
      Brush brush = new SolidBrush(lineInfoColor);

      int maxLine = GetGridBytePoint(endByte - startByte).Y + 1;

      for (int i = 0; i < maxLine; i++)
      {
        long firstLineByte = (startByte + (_iHexMaxHBytes) * i) + _lineInfoOffset;

        PointF bytePointF = GetBytePointF(new Point(0, 0 + i));
        string info = firstLineByte.ToString(_hexStringFormat, System.Threading.Thread.CurrentThread.CurrentCulture);
        int nulls = 8 - info.Length;
        string formattedInfo;

        if (nulls > -1)
          formattedInfo = new string('0', 8 - info.Length) + info;
        else
          formattedInfo = new string('~', 8);

        g.DrawString(formattedInfo, Font, brush, new PointF(_recLineInfo.X, bytePointF.Y), _stringFormat);
      }
    }

    private void UpdateVisibilityBytes()
    {
      if (_byteProvider == null || _byteProvider.Length == 0)
        return;

      _startByte = (_scrollVpos + 1) * _iHexMaxHBytes - _iHexMaxHBytes;
      _endByte = (long)Math.Min(_byteProvider.Length - 1, _startByte + _iHexMaxBytes);
    }

    #endregion Paint methods

    #region Positioning methods

    private PointF GetBytePointF(long byteIndex)
    {
      Point gp = GetGridBytePoint(byteIndex);
      return GetBytePointF(gp);
    }

    private PointF GetBytePointF(Point gp)
    {
      float x = (3 * _charSize.Width) * gp.X + _recHex.X;
      float y = (gp.Y + 1) * _charSize.Height - _charSize.Height + _recHex.Y;

      return new PointF(x, y);
    }

    private PointF GetByteStringPointF(Point gp)
    {
      float x = (_charSize.Width) * gp.X + _recStringView.X;
      float y = (gp.Y + 1) * _charSize.Height - _charSize.Height + _recStringView.Y;

      return new PointF(x, y);
    }

    private PointF GetColumnInfoPointF(int col)
    {
      Point gp = GetGridBytePoint(col);
      float x = (3 * _charSize.Width) * gp.X + _recColumnInfo.X;
      float y = _recColumnInfo.Y;

      return new PointF(x, y);
    }

    private Point GetGridBytePoint(long byteIndex)
    {
      int row = (int)Math.Floor((double)byteIndex / (double)_iHexMaxHBytes);
      int column = (int)(byteIndex + _iHexMaxHBytes - _iHexMaxHBytes * (row + 1));

      Point res = new Point(column, row);
      return res;
    }

    private void UpdateRectanglePositioning()
    {
      // calc char size
      SizeF charSize;
      using (var graphics = this.CreateGraphics())
      {
        charSize = this.CreateGraphics().MeasureString("A", Font, 100, _stringFormat);
      }
      CharSize = new SizeF((float)Math.Ceiling(charSize.Width), (float)Math.Ceiling(charSize.Height));

      int requiredWidth = 0;

      // calc content bounds
      _recContent = ClientRectangle;
      _recContent.X += _recBorderLeft;
      _recContent.Y += _recBorderTop;
      _recContent.Width -= _recBorderRight + _recBorderLeft;
      _recContent.Height -= _recBorderBottom + _recBorderTop;

      if (_vScrollBarVisible)
      {
        _recContent.Width -= _vScrollBar.Width;
        _vScrollBar.Left = _recContent.X + _recContent.Width;
        _vScrollBar.Top = _recContent.Y;
        _vScrollBar.Height = _recContent.Height;
        requiredWidth += _vScrollBar.Width;
      }

      int marginLeft = 4;

      // calc line info bounds
      if (_lineInfoVisible)
      {
        _recLineInfo = new Rectangle(_recContent.X + marginLeft,
          _recContent.Y,
          (int)(_charSize.Width * 10),
          _recContent.Height);

        requiredWidth += _recLineInfo.Width;
      }
      else
      {
        _recLineInfo = Rectangle.Empty;
        _recLineInfo.X = marginLeft;
        requiredWidth += marginLeft;
      }

      // calc line info bounds
      _recColumnInfo = new Rectangle(_recLineInfo.X + _recLineInfo.Width, _recContent.Y, _recContent.Width - _recLineInfo.Width, (int)charSize.Height + 4);
      if (_columnInfoVisible)
      {
        _recLineInfo.Y += (int)charSize.Height + 4;
        _recLineInfo.Height -= (int)charSize.Height + 4;
      }
      else
      {
        _recColumnInfo.Height = 0;
      }

      // calc hex bounds and grid
      _recHex = new Rectangle(_recLineInfo.X + _recLineInfo.Width, _recLineInfo.Y, _recContent.Width - _recLineInfo.Width, _recContent.Height - _recColumnInfo.Height);
      if (UseFixedBytesPerLine)
      {
        SetHorizontalByteCount(_bytesPerLine);
        _recHex.Width = (int)Math.Floor(((double)_iHexMaxHBytes) * _charSize.Width * 3 + (2 * _charSize.Width));
        requiredWidth += _recHex.Width;
      }
      else
      {
        int hmax = (int)Math.Floor((double)_recHex.Width / (double)_charSize.Width);
        if (_stringViewVisible)
        {
          hmax -= 2;
          if (hmax > 1)
            SetHorizontalByteCount((int)Math.Floor((double)hmax / 4));
          else
            SetHorizontalByteCount(1);
        }
        else
        {
          if (hmax > 1)
            SetHorizontalByteCount((int)Math.Floor((double)hmax / 3));
          else
            SetHorizontalByteCount(1);
        }
        _recHex.Width = (int)Math.Floor(((double)_iHexMaxHBytes) * _charSize.Width * 3 + (2 * _charSize.Width));
        requiredWidth += _recHex.Width;
      }

      if (_stringViewVisible)
      {
        _recStringView = new Rectangle(_recHex.X + _recHex.Width,
          _recHex.Y,
          (int)(_charSize.Width * _iHexMaxHBytes),
          _recHex.Height);
        requiredWidth += _recStringView.Width;
      }
      else
      {
        _recStringView = Rectangle.Empty;
      }

      RequiredWidth = requiredWidth;

      int vmax = (int)Math.Floor((double)_recHex.Height / (double)_charSize.Height);
      SetVerticalByteCount(vmax);

      _iHexMaxBytes = _iHexMaxHBytes * _iHexMaxVBytes;

      UpdateScrollSize();
    }

    #endregion Positioning methods

    #region Overridden properties

    /// <summary>
    /// Gets or sets the background color for the control.
    /// </summary>
    [DefaultValue(typeof(Color), "White")]
    public override Color BackColor
    {
      get => base.BackColor;
      set => base.BackColor = value;
    }

    /// <summary>
    /// The font used to display text in the hexbox.
    /// </summary>
    public override Font Font
    {
      get => base.Font;

      set
      {
        if (value == null)
          return;

        base.Font = value;
        this.UpdateRectanglePositioning();
        this.Invalidate();
      }
    }

    /// <summary>
    /// Not used.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never), Bindable(false)]
    public override RightToLeft RightToLeft
    {
      get => base.RightToLeft;

      set => base.RightToLeft = value;
    }

    /// <summary>Not used.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never), Bindable(false)]
    public override string Text
    {
      get => base.Text;
      set => base.Text = value;
    }

    #endregion Overridden properties

    #region Properties

    private Color _backColorDisabled = Color.FromName("WhiteSmoke");
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private BuiltInContextMenu _builtInContextMenu;
    private IByteCharConverter _byteCharConverter;
    private IByteProvider _byteProvider;
    private int _bytesPerLine = 16;
    private SizeF _charSize;
    private bool _columnInfoVisible = false;
    private long _currentLine;
    private int _currentPositionInLine;
    private bool _groupSeparatorVisible = false;
    private int _groupSize = 4;
    private Color _infoForeColor = Color.Gray;
    private long _lineInfoOffset = 0;
    private bool _lineInfoVisible = false;
    private bool _readOnly;
    private int _requiredWidth;
    private Color _selectionBackColor = Color.Blue;
    private Color _selectionForeColor = Color.White;
    private long _selectionLength;
    private Color _shadowSelectionColor = Color.FromArgb(100, 60, 188, 255);
    private bool _shadowSelectionVisible = true;
    private bool _stringViewVisible;
    private bool _useFixedBytesPerLine;
    private bool _vScrollBarVisible;

    /// <summary>Gets or sets the background color for the disabled control.</summary>
    [Category("Appearance"), DefaultValue(typeof(Color), "WhiteSmoke")]
    public Color BackColorDisabled
    {
      get => _backColorDisabled;
      set => _backColorDisabled = value;
    }

    /// <summary>Gets or sets the hex box�s border style.</summary>
    [DefaultValue(typeof(BorderStyle), "Fixed3D"), Category("Hex"), Description("Gets or sets the hex box�s border style.")]
    public BorderStyle BorderStyle
    {
      get => _borderStyle;
      set
      {
        if (_borderStyle == value)
          return;

        _borderStyle = value;
        switch (_borderStyle)
        {
          case BorderStyle.None:
            _recBorderLeft = _recBorderTop = _recBorderRight = _recBorderBottom = 0;
            break;

          case BorderStyle.Fixed3D:
            _recBorderLeft = _recBorderRight = SystemInformation.Border3DSize.Width;
            _recBorderTop = _recBorderBottom = SystemInformation.Border3DSize.Height;
            break;

          case BorderStyle.FixedSingle:
            _recBorderLeft = _recBorderTop = _recBorderRight = _recBorderBottom = 1;
            break;
        }

        UpdateRectanglePositioning();

        OnBorderStyleChanged(EventArgs.Empty);
      }
    }

    /// <summary>Gets or sets the built-in context menu.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public BuiltInContextMenu BuiltInContextMenu => _builtInContextMenu;

    /// <summary>Gets or sets the converter that will translate between byte and character values.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IByteCharConverter ByteCharConverter
    {
      get
      {
        if (_byteCharConverter == null)
          _byteCharConverter = new DefaultByteCharConverter();
        return _byteCharConverter;
      }

      set
      {
        if (value != null && value != _byteCharConverter)
        {
          _byteCharConverter = value;
          Invalidate();
        }
      }
    }

    /// <summary>Gets or sets the ByteProvider.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IByteProvider ByteProvider
    {
      get => _byteProvider;
      set
      {
        if (_byteProvider == value)
          return;

        if (value == null)
          ActivateEmptyKeyInterpreter();
        else
          ActivateKeyInterpreter();

        if (_byteProvider != null)
          _byteProvider.LengthChanged -= new EventHandler(_byteProvider_LengthChanged);

        _byteProvider = value;
        if (_byteProvider != null)
          _byteProvider.LengthChanged += new EventHandler(_byteProvider_LengthChanged);

        OnByteProviderChanged(EventArgs.Empty);

        if (value == null) // do not raise events if value is null
        {
          _bytePos = -1;
          _byteCharacterPos = 0;
          _selectionLength = 0;

          DestroyCaret();
        }
        else
        {
          SetPosition(0, 0);
          SetSelectionLength(0);

          if (_caretVisible && Focused)
            UpdateCaret();
          else
            CreateCaret();
        }

        CheckCurrentLineChanged();
        CheckCurrentPositionInLineChanged();

        _scrollVpos = 0;

        UpdateVisibilityBytes();
        UpdateRectanglePositioning();

        Invalidate();
      }
    }

    /// <summary>Gets or sets the maximum count of bytes in one line.</summary>
    /// <remarks>UseFixedBytesPerLine property no longer has to be set to true for this to work</remarks>
    [DefaultValue(16), Category("Hex"), Description("Gets or sets the maximum count of bytes in one line.")]
    public int BytesPerLine
    {
      get => _bytesPerLine;
      set
      {
        if (_bytesPerLine == value)
          return;

        _bytesPerLine = value;
        OnBytesPerLineChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Contains the size of a single character in pixel</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SizeF CharSize
    {
      get => _charSize;
      private set
      {
        if (_charSize == value)
          return;

        _charSize = value;

        if (CharSizeChanged != null)
          CharSizeChanged(this, EventArgs.Empty);
      }
    }

    /// <summary>Gets or sets the visibility of the column info.</summary>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of header row.")]
    public bool ColumnInfoVisible
    {
      get => _columnInfoVisible;
      set
      {
        if (_columnInfoVisible == value)
          return;

        _columnInfoVisible = value;
        OnColumnInfoVisibleChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Gets the current line.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long CurrentLine => _currentLine;

    /// <summary>Gets the current position in the current line.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long CurrentPositionInLine => _currentPositionInLine;

    /// <summary>Gets or sets the visibility of the group separator.</summary>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of a separator vertical line.")]
    public bool GroupSeparatorVisible
    {
      get => _groupSeparatorVisible;
      set
      {
        if (_groupSeparatorVisible == value)
          return;

        _groupSeparatorVisible = value;
        OnGroupSeparatorVisibleChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Gets or sets the number of bytes in a group. Used to show the group separator line (if GroupSeparatorVisible is true).</summary>
    /// <remarks>GroupSeparatorVisible property must set to true.</remarks>
    [DefaultValue(4), Category("Hex"), Description("Gets or sets the byte-count between group separators (if visible).")]
    public int GroupSize
    {
      get => _groupSize;
      set
      {
        if (_groupSize == value)
          return;

        _groupSize = value;
        OnGroupSizeChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Gets or sets whether the HexBox control displays the hex characters in upper or lower case.</summary>
    [DefaultValue(typeof(HexCasing), "Upper"), Category("Hex"), Description("Gets or sets whether the HexBox control displays the hex characters in upper or lower case.")]
    public HexCasing HexCasing
    {
      get => _hexStringFormat == "X" ? HexCasing.Upper : HexCasing.Lower;
      set
      {
        string format = value == HexCasing.Upper ? "X" : "x";

        if (_hexStringFormat == format)
          return;

        _hexStringFormat = format;
        OnHexCasingChanged(EventArgs.Empty);

        Invalidate();
      }
    }

    /// <summary>Gets the number bytes drawn horizontally.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int HorizontalByteCount => _iHexMaxHBytes;

    /// <summary>Gets or sets the info color used for column info and line info. When this property is null, then ForeColor property is used.</summary>
    [DefaultValue(typeof(Color), "Gray"), Category("Hex"), Description("Gets or sets the line info color. When this property is null, then ForeColor property is used.")]
    public Color InfoForeColor
    {
      get => _infoForeColor;
      set
      {
        _infoForeColor = value;
        Invalidate();
      }
    }

    /// <summary>Gets the a value if insertion mode is active or not.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool InsertActive
    {
      get => _insertActive;
      set
      {
        if (_insertActive == value)
          return;

        _insertActive = value;

        // recreate caret
        DestroyCaret();
        CreateCaret();

        // raise change event
        OnInsertActiveChanged(EventArgs.Empty);
      }
    }

    /// <summary>
    /// Gets or sets the offset of a line info.
    /// </summary>
    [DefaultValue((long)0), Category("Hex"), Description("Gets or sets the offset of the line info.")]
    public long LineInfoOffset
    {
      get => _lineInfoOffset;
      set
      {
        if (_lineInfoOffset == value)
          return;

        _lineInfoOffset = value;

        Invalidate();
      }
    }

    /// <summary>Gets or sets the visibility of a line info.</summary>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of a line info.")]
    public bool LineInfoVisible
    {
      get => _lineInfoVisible;
      set
      {
        if (_lineInfoVisible == value)
          return;

        _lineInfoVisible = value;
        OnLineInfoVisibleChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Gets or sets if the count of bytes in one line is fix.</summary>
    /// <remarks>When set to True, BytesPerLine property determine the maximum count of bytes in one line.</remarks>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets if the count of bytes in one line is fix.")]
    public bool ReadOnly
    {
      get => _readOnly;
      set
      {
        if (_readOnly == value)
          return;

        _readOnly = value;
        OnReadOnlyChanged(EventArgs.Empty);
        Invalidate();
      }
    }

    /// <summary>Gets the width required for the content.</summary>
    [DefaultValue(0), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int RequiredWidth
    {
      get => _requiredWidth;

      private set
      {
        if (_requiredWidth == value)
          return;

        _requiredWidth = value;

        if (RequiredWidthChanged != null)
          RequiredWidthChanged(this, EventArgs.Empty);
      }
    }

    /// <summary>Gets or sets the background color for the selected bytes.</summary>
    [DefaultValue(typeof(Color), "Blue"), Category("Hex"), Description("Gets or sets the background color for the selected bytes.")]
    public Color SelectionBackColor
    {
      get => _selectionBackColor;
      set
      {
        _selectionBackColor = value;
        Invalidate();
      }
    }

    /// <summary>Gets or sets the foreground color for the selected bytes.</summary>
    [DefaultValue(typeof(Color), "White"), Category("Hex"), Description("Gets or sets the foreground color for the selected bytes.")]
    public Color SelectionForeColor
    {
      get => _selectionForeColor;
      set
      {
        _selectionForeColor = value;
        Invalidate();
      }
    }

    /// <summary>Gets and sets the number of bytes selected in the hex box.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long SelectionLength
    {
      get => _selectionLength;
      set
      {
        SetSelectionLength(value);
        ScrollByteIntoView();
        Invalidate();
      }
    }

    /// <summary>Gets and sets the starting point of the bytes selected in the hex box.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long SelectionStart
    {
      get => _bytePos;
      set
      {
        SetPosition(value, 0);
        ScrollByteIntoView();
        Invalidate();
      }
    }

    /// <summary>Gets or sets the color of the shadow selection.</summary>
    /// <remarks>
    ///   A alpha component must be given!
    ///   Default alpha = 100
    /// </remarks>
    [Category("Hex"), Description("Gets or sets the color of the shadow selection.")]
    public Color ShadowSelectionColor
    {
      get => _shadowSelectionColor;
      set
      {
        _shadowSelectionColor = value;
        Invalidate();
      }
    }

    /// <summary>Gets or sets the visibility of a shadow selection.</summary>
    [DefaultValue(true), Category("Hex"), Description("Gets or sets the visibility of a shadow selection.")]
    public bool ShadowSelectionVisible
    {
      get => _shadowSelectionVisible;

      set
      {
        if (_shadowSelectionVisible == value)
          return;

        _shadowSelectionVisible = value;
        Invalidate();
      }
    }

    /// <summary>Gets or sets the visibility of the string view.</summary>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of the string view.")]
    public bool StringViewVisible
    {
      get => _stringViewVisible;

      set
      {
        if (_stringViewVisible == value)
          return;

        _stringViewVisible = value;
        OnStringViewVisibleChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Gets or sets if the count of bytes in one line is fix.</summary>
    /// <remarks>When set to True, BytesPerLine property determine the maximum count of bytes in one line.</remarks>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets if the count of bytes in one line is fix.")]
    public bool UseFixedBytesPerLine
    {
      get => _useFixedBytesPerLine;
      set
      {
        if (_useFixedBytesPerLine == value)
          return;

        _useFixedBytesPerLine = value;
        OnUseFixedBytesPerLineChanged(EventArgs.Empty);

        UpdateRectanglePositioning();
        Invalidate();
      }
    }

    /// <summary>Gets the number bytes drawn vertically.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int VerticalByteCount => _iHexMaxVBytes;

    /// <summary>Gets or sets the visibility of a vertical scroll bar.</summary>
    [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of a vertical scroll bar.")]
    public bool VScrollBarVisible
    {
      get { return this._vScrollBarVisible; }
      set
      {
        if (_vScrollBarVisible == value)
          return;

        _vScrollBarVisible = value;

        if (_vScrollBarVisible)
          Controls.Add(_vScrollBar);
        else
          Controls.Remove(_vScrollBar);

        UpdateRectanglePositioning();
        UpdateScrollSize();

        OnVScrollBarVisibleChanged(EventArgs.Empty);
      }
    }

    #endregion Properties

    #region Misc

    /// <summary>Raises the BorderStyleChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnBorderStyleChanged(EventArgs e)
    {
      BorderStyleChanged?.Invoke(this, e);
    }

    /// <summary>Raises the ByteProviderChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnByteProviderChanged(EventArgs e)
    {
      ByteProviderChanged?.Invoke(this, e);
    }

    /// <summary>Raises the BytesPerLineChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnBytesPerLineChanged(EventArgs e)
    {
      BytesPerLineChanged?.Invoke(this, e);
    }

    /// <summary>Raises the OnColumnInfoVisibleChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnColumnInfoVisibleChanged(EventArgs e)
    {
      ColumnInfoVisibleChanged?.Invoke(this, e);
    }

    /// <summary>Raises the Copied event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnCopied(EventArgs e)
    {
      Copied?.Invoke(this, e);
    }

    /// <summary>Raises the CopiedHex event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnCopiedHex(EventArgs e)
    {
      CopiedHex?.Invoke(this, e);
    }

    /// <summary>Raises the CurrentLineChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnCurrentLineChanged(EventArgs e)
    {
      CurrentLineChanged?.Invoke(this, e);
    }

    /// <summary>Raises the CurrentPositionInLineChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnCurrentPositionInLineChanged(EventArgs e)
    {
      CurrentPositionInLineChanged?.Invoke(this, e);
    }

    /// <summary>Raises the GotFocus event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected override void OnGotFocus(EventArgs e)
    {
      System.Diagnostics.Debug.WriteLine("OnGotFocus()", "HexBox");

      base.OnGotFocus(e);

      CreateCaret();
    }

    /// <summary>Raises the ColumnSeparatorVisibleChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnGroupSeparatorVisibleChanged(EventArgs e)
    {
      GroupSeparatorVisibleChanged?.Invoke(this, e);
    }

    /// <summary>Raises the GroupSizeChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnGroupSizeChanged(EventArgs e)
    {
      GroupSizeChanged?.Invoke(this, e);
    }

    /// <summary>Raises the HexCasingChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnHexCasingChanged(EventArgs e)
    {
      HexCasingChanged?.Invoke(this, e);
    }

    /// <summary>Raises the HorizontalByteCountChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnHorizontalByteCountChanged(EventArgs e)
    {
      HorizontalByteCountChanged?.Invoke(this, e);
    }

    /// <summary>Raises the InsertActiveChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnInsertActiveChanged(EventArgs e)
    {
      InsertActiveChanged?.Invoke(this, e);
    }

    /// <summary>Raises the LineInfoVisibleChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnLineInfoVisibleChanged(EventArgs e)
    {
      LineInfoVisibleChanged?.Invoke(this, e);
    }

    /// <summary>Raises the LostFocus event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected override void OnLostFocus(EventArgs e)
    {
      System.Diagnostics.Debug.WriteLine("OnLostFocus()", "HexBox");
      base.OnLostFocus(e);
      DestroyCaret();
    }

    /// <summary>Raises the MouseDown event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
      System.Diagnostics.Debug.WriteLine("OnMouseDown()", "HexBox");

      if (!Focused)
        Focus();

      if (e.Button == MouseButtons.Left)
        SetCaretPosition(new Point(e.X, e.Y));

      base.OnMouseDown(e);
    }

    /// <summary>Raises the MouseWhell event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
      int linesToScroll = -(e.Delta * SystemInformation.MouseWheelScrollLines / 120);
      PerformScrollLines(linesToScroll);

      base.OnMouseWheel(e);
    }

    /// <summary>Raises the ReadOnlyChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnReadOnlyChanged(EventArgs e)
    {
      ReadOnlyChanged?.Invoke(this, e);
    }

    /// <summary>Raises the Resize event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected override void OnResize(EventArgs e)
    {
      base.OnResize(e);
      UpdateRectanglePositioning();
    }

    /// <summary>Raises the SelectionLengthChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnSelectionLengthChanged(EventArgs e)
    {
      SelectionLengthChanged?.Invoke(this, e);
    }

    /// <summary>Raises the SelectionStartChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnSelectionStartChanged(EventArgs e)
    {
      SelectionStartChanged?.Invoke(this, e);
    }

    /// <summary>Raises the StringViewVisibleChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnStringViewVisibleChanged(EventArgs e)
    {
      StringViewVisibleChanged?.Invoke(this, e);
    }

    /// <summary>Raises the UseFixedBytesPerLineChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnUseFixedBytesPerLineChanged(EventArgs e)
    {
      UseFixedBytesPerLineChanged?.Invoke(this, e);
    }

    /// <summary>Raises the VerticalByteCountChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnVerticalByteCountChanged(EventArgs e)
    {
      VerticalByteCountChanged?.Invoke(this, e);
    }

    /// <summary>Raises the VScrollBarVisibleChanged event.</summary>
    /// <param name="e">An EventArgs that contains the event data.</param>
    protected virtual void OnVScrollBarVisibleChanged(EventArgs e)
    {
      VScrollBarVisibleChanged?.Invoke(this, e);
    }

    private void _byteProvider_LengthChanged(object sender, EventArgs e)
    {
      UpdateScrollSize();
    }

    private void CheckCurrentLineChanged()
    {
      long currentLine = (long)Math.Floor((double)_bytePos / (double)_iHexMaxHBytes) + 1;

      if (_byteProvider == null && _currentLine != 0)
      {
        _currentLine = 0;
        OnCurrentLineChanged(EventArgs.Empty);
      }
      else if (currentLine != _currentLine)
      {
        _currentLine = currentLine;
        OnCurrentLineChanged(EventArgs.Empty);
      }
    }

    private void CheckCurrentPositionInLineChanged()
    {
      Point gb = GetGridBytePoint(_bytePos);
      int currentPositionInLine = gb.X + 1;

      if (_byteProvider == null && _currentPositionInLine != 0)
      {
        _currentPositionInLine = 0;
        OnCurrentPositionInLineChanged(EventArgs.Empty);
      }
      else if (currentPositionInLine != _currentPositionInLine)
      {
        _currentPositionInLine = currentPositionInLine;
        OnCurrentPositionInLineChanged(EventArgs.Empty);
      }
    }

    /// <summary>Converts a byte array to a hex string. For example: {10,11} = "0A 0B"</summary>
    /// <param name="data">the byte array</param>
    /// <returns>The hex string.</returns>
    private string ConvertBytesToHex(byte[] data)
    {
      StringBuilder sb = new StringBuilder();
      foreach (byte b in data)
      {
        string hex = ConvertByteToHex(b);
        sb.Append(hex);
        sb.Append(' ');
      }

      if (sb.Length > 0)
        sb.Remove(sb.Length - 1, 1);

      string result = sb.ToString();
      return result;
    }

    /// <summary>Converts the byte to a hex string. For example: "10" = "0A";</summary>
    /// <param name="b">the byte to format</param>
    /// <returns>the hex string</returns>
    private string ConvertByteToHex(byte b)
    {
      string sB = b.ToString(_hexStringFormat, System.Threading.Thread.CurrentThread.CurrentCulture);
      if (sB.Length == 1)
        sB = "0" + sB;

      return sB;
    }

    private bool ConvertHexToByte(string hex, out byte b)
    {
      return byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Threading.Thread.CurrentThread.CurrentCulture, out b);
    }

    /// <summary>
    ///   Converts the hex string to an byte array. The hex string must be separated by a space char ' '.
    ///   If there is any invalid hex information in the string the result will be null.
    /// </summary>
    /// <param name="hex">the hex string separated by ' '. For example: "0A 0B 0C"</param>
    /// <returns>the byte array. null if hex is invalid or empty</returns>
    private byte[] ConvertHexToBytes(string hex)
    {
      if (string.IsNullOrEmpty(hex))
        return null;

      hex = hex.Trim();
      var hexArray = hex.Split(' ');
      var byteArray = new byte[hexArray.Length];

      for (int i = 0; i < hexArray.Length; i++)
      {
        var hexValue = hexArray[i];

        byte b;
        var isByte = ConvertHexToByte(hexValue, out b);
        if (!isByte)
          return null;
        byteArray[i] = b;
      }

      return byteArray;
    }

    private void SetHorizontalByteCount(int value)
    {
      if (_iHexMaxHBytes == value)
        return;

      _iHexMaxHBytes = value;
      OnHorizontalByteCountChanged(EventArgs.Empty);
    }

    private void SetPosition(long bytePos)
    {
      SetPosition(bytePos, _byteCharacterPos);
    }

    private void SetPosition(long bytePos, int byteCharacterPos)
    {
      if (_byteCharacterPos != byteCharacterPos)
        _byteCharacterPos = byteCharacterPos;

      if (bytePos != _bytePos)
      {
        _bytePos = bytePos;
        CheckCurrentLineChanged();
        CheckCurrentPositionInLineChanged();

        OnSelectionStartChanged(EventArgs.Empty);
      }
    }

    private void SetSelectionLength(long selectionLength)
    {
      if (selectionLength != _selectionLength)
      {
        _selectionLength = selectionLength;
        OnSelectionLengthChanged(EventArgs.Empty);
      }
    }

    private void SetVerticalByteCount(int value)
    {
      if (_iHexMaxVBytes == value)
        return;

      _iHexMaxVBytes = value;
      OnVerticalByteCountChanged(EventArgs.Empty);
    }

    #endregion Misc

    #region Scaling Support for High DPI resolution screens

    /// <summary>For high resolution screen support.</summary>
    /// <param name="factor">The size factor.</param>
    /// <param name="specified">Bounds specified.</param>
    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
      base.ScaleControl(factor, specified);

      BeginInvoke(new MethodInvoker(() =>
      {
        UpdateRectanglePositioning();
        if (_caretVisible)
        {
          DestroyCaret();
          CreateCaret();
        }

        Invalidate();
      }));
    }

    #endregion Scaling Support for High DPI resolution screens
  }
}
