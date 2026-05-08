using System.Text;

namespace Zhengyan.Commons
{
    public class AdvancedConsole
    {
        public ConsoleColor BackgroundColor { get; set; }
        public ConsoleColor ForegroundColor { get; set; }

        public string StartTag { get; set; }
        public string EndTag { get; set; }
        public AdvancedConsole()
        {
            StartTag = "&^";

            EndTag = "^&";

            BackgroundColor = ConsoleColor.Yellow;
            ForegroundColor = ConsoleColor.Black;
        }

        public void Write(string value)
        {
            ConsoleColor sbc = Console.BackgroundColor;
            ConsoleColor sfc = Console.ForegroundColor;

            int cur = 0;
            while (true)
            {
                if (cur >= value.Length)
                    break;
                int lcur = cur;
                cur = value.IndexOf(StartTag, cur);

                if (cur > -1)
                {
                    int start = cur + StartTag.Length;
                    int end = value.IndexOf(EndTag, start);
                    if (end < 0)
                    {
                        Console.Write(value.Substring(lcur));
                        break;
                    }
                    else
                    {
                        Console.Write(value.Substring(lcur, cur - lcur));
                        Console.BackgroundColor = BackgroundColor;
                        Console.ForegroundColor = ForegroundColor;
                        Console.Write(value.Substring(start, end - start));
                        cur = end + EndTag.Length;
                        Console.BackgroundColor = sbc;
                        Console.ForegroundColor = sfc;
                    }
                }
                else
                {
                    Console.Write(value.Substring(lcur));
                    break;
                }
            }


            Console.BackgroundColor = sbc;
            Console.ForegroundColor = sfc;
        }

        public void WriteLine(string value)
        {
            Write(value + '\n');
        }

        public string ReadText(string end)
        {
            ulong endVal = GetCheckSum(end);
            ulong maskVal = GetMask(sizeof(char) * end.Length);
            endVal &= maskVal;

            ulong retVal = 0;

            StringBuilder readText = new StringBuilder();
            while (true)
            {
                char ch = (char)Console.Read();
                readText.Append(ch);
                retVal = GetCheckSum(ch, retVal) & maskVal;

                if (retVal == endVal)
                    break;
            }

            readText.Remove(readText.Length - end.Length, end.Length);
            return readText.ToString();
        }

        public static ulong GetMask(int len)
        {
            ulong v = 0;
            for (int i = 0; i < len; i++)
                v = v << 8 | 0xff;
            return v;
        }

        public static ulong GetCheckSum(char c, ulong hash = 0)
        {
            int offset = 8 * sizeof(char);
            return hash << offset | (uint)c;
        }

        public static ulong GetCheckSum(string str, ulong hash = 0)
        {
            foreach (char c in str)
                hash = GetCheckSum(c, hash);
            return hash;
        }
    }
}