using System.Text;

namespace Zhengyan.WebSearch.Extensions;
public static class ExtensionMethods
{
    public static Encoding GetEncoding(this string encodingName, string defaultEncodingName = "utf-8")
    {
        try
        {
            return Encoding.GetEncoding(encodingName);
        }
        catch (Exception ex)
        {
            return Encoding.GetEncoding(defaultEncodingName);
        }
    }
}