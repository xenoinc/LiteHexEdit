namespace Lite.Windows.Forms.Converters
{
  /// <summary>
  /// The interface for objects that can translate between characters and bytes.
  /// </summary>
  /// <remarks>
  ///   https://docs.microsoft.com/en-us/dotnet/api/system.text.encodinginfo.getencoding?view=net-6.0
  /// </remarks>
  public interface IByteCharConverter
  {
    /// <summary>
    /// Returns the byte to use when the character passed across is entered during editing.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    byte ToByte(char c);

    /// <summary>
    /// Returns the character to display for the byte passed across.
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    char ToChar(byte b);
  }
}
