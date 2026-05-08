using System;

namespace Zhengyan.Commons
{
    public static class TextUtils
    {
        // private static TextProcessPipeline processPipeline;

        static TextUtils()
        {
            // processPipeline = new TextProcessPipeline();
            // processPipeline.AddHandler(new ExtractValidCharFromHtmlHandler());
        }

        public static string ExtractValidTextFromHtml(string html)
        {
            var processPipeline = new TextProcessPipeline();
            processPipeline.AddHandler(new ExtractValidCharFromHtmlHandler());
            return processPipeline.ProcessText(html)[0].ToString();
        }

        public unsafe static void Reverse(this string str)
        {
            int len = str.Length;
            if(len < 1)
                return;
            fixed(char* ptr = str)
            {
                char* sptr = ptr;
                char* eptr = sptr + len - 1;
                char t;
                while(sptr < eptr)
                {
                    t = *sptr;
                    *sptr = *eptr;
                    *eptr = t;

                    sptr++;
                    eptr--;
                }
            }
        }
    }
}