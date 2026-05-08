using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zhengyan.Commons
{
    public static class ExtensionMethods
    {
        public static string Repeat(this string str, int n)
        {
            if (n < 0)
                n = 0;
            char[] arr = str.ToCharArray();

            char[] arrDest = new char[arr.Length * n];

            for (int i = 0; i < n; i++)
            {

                Buffer.BlockCopy(arr, 0, arrDest, i * arr.Length * 2, arr.Length * 2);

            }

            return new string(arrDest);
        }

        public static string Repeat(this char c, int n)
        {
            if (n < 0)
                n = 0;
            char[] arrDest = new char[n];

            for (int i = 0; i < n; i++)
            {
                arrDest[i] = c;
            }

            return new string(arrDest);
        }

        public static string ToHexString(this byte[] bytes)
        {
            return string.Join("", bytes.Select(b => string.Format("{0:X2}", b)));
        }

        public static byte[] HexStringToBytes(this string hexString)
        {
            var hexs = hexString.AsSpan();
            byte[] bytes = new byte[hexs.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexs.Slice(i * 2, 2).ToString(), 16);
            }
            return bytes;
        }
    }

}
