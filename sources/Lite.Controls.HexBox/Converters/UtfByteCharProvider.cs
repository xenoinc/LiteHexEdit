using System.Text;

namespace Lite.Windows.Forms.Converters
{
  /// <summary>
  /// A byte char provider that can translate bytes encoded in codepage 500 EBCDIC
  /// </summary>
  public class UtfByteCharProvider : IByteCharConverter
  {
    /// <summary>
    /// The Unicode (UTF-16) code page 1200 encoding.
    /// </summary>
    private Encoding _encoding = Encoding.GetEncoding(1200);

    /// <summary>
    /// Returns the byte corresponding to the EBCDIC character passed across.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    public virtual byte ToByte(char c)
    {
      byte[] decoded = _encoding.GetBytes(new char[] { c });
      return decoded.Length > 0 ? decoded[0] : (byte)0;
    }

    /// <summary>
    /// Returns the EBCDIC character corresponding to the byte passed across.
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public virtual char ToChar(byte b)
    {
      string encoded = _encoding.GetString(new byte[] { b });
      return encoded.Length > 0 ? encoded[0] : '.';
    }

    /// <summary>
    /// Returns a description of the byte char provider.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return "UTF (Code Page 1200)";
    }
  }
}
