using System.Text;

namespace Lite.Windows.Forms.Converters
{
  /// <summary>
  /// A byte char provider that can translate bytes encoded in code page 500 EBCDIC
  /// </summary>
  public class EbcdicByteCharProvider : IByteCharConverter
  {
    /// <summary>
    /// The IBM EBCDIC code page 500 encoding. Note that this is not always supported by .NET,
    /// the underlying platform has to provide support for it.
    /// </summary>
    private Encoding _ebcdicEncoding = Encoding.GetEncoding(500);

    /// <summary>
    /// Returns the byte corresponding to the EBCDIC character passed across.
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    public virtual byte ToByte(char c)
    {
      byte[] decoded = _ebcdicEncoding.GetBytes(new char[] { c });
      return decoded.Length > 0 ? decoded[0] : (byte)0;
    }

    /// <summary>
    /// Returns the EBCDIC character corresponding to the byte passed across.
    /// </summary>
    /// <param name="b"></param>
    /// <returns></returns>
    public virtual char ToChar(byte b)
    {
      string encoded = _ebcdicEncoding.GetString(new byte[] { b });
      return encoded.Length > 0 ? encoded[0] : '.';
    }

    /// <summary>
    /// Returns a description of the byte char provider.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
      return "EBCDIC (Code Page 500)";
    }
  }
}
