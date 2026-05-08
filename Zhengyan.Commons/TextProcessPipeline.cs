using System;
using System.Collections.Generic;

namespace Zhengyan.Commons
{
    public class TextProcessPipeline
    {
        private List<ITextHandler> handlers = new List<ITextHandler>();

        public TextProcessPipeline RemoveHandler(int index)
        {
            handlers.RemoveAt(index);
            return this;
        }

        public TextProcessPipeline AddHandler(ITextHandler handler)
        {
            handlers.Add(handler);
            return this;
        }

        public unsafe List<object> ProcessText(string text)
        {
            int len = text.Length;
            handlers.ForEach((h)=>h.Begin());
            fixed(char* p_text = text)
            {
                for(int i = 0;i < len;i++)
                {
                    char ch = *(p_text + i);
                    // Console.WriteLine(ch);
                    handlers.ForEach((h)=>h.ProcessChar(ch,i));
                }
            }
            handlers.ForEach((h)=>h.End());

            List<object> results = new List<object>();
            handlers.ForEach((h)=>results.Add(h.GetResult()));
            return results;
        }
    }
}
