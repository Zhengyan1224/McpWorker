using System.Text;
using System.Collections.Generic;
using System;

namespace Zhengyan.Commons
{
    public enum Decision
    {
        Include,
        Exclude
    }

    public class ExtractValidCharFromHtmlHandler : ITextHandler
    {
        private StringBuilder result;
        private string speccharstr = "!@#$%^&*()_+{}[]:\";'<>,./?\\ \t|`~-=\n【】；：“”‘’，。、《》？\r";
        // private string speccharstr = "\t\n\r|#;";
        private HashSet<char> specchars;
        // private char insertSplitChar = '|';
        private char insertSplitChar = ' ';

        public Decision Decision { get; set; }

        private HashSet<string> compareLabelNames = new HashSet<string>();

        public HashSet<string> CompareLabelNames => compareLabelNames;

        public ExtractValidCharFromHtmlHandler()
        {
            specchars = new HashSet<char>();
            specchars.UnionWith(speccharstr);
            specchars.Add((char)160);
            specchars.Add((char)12288);
            Decision = Decision.Include;
            compareLabelNames.Add("title");
            compareLabelNames.Add("meta");
            compareLabelNames.Add("a");
        }

        public void Begin()
        {
            if (result != null)
            {
                result.Clear();
                result = null;
            }
            result = new StringBuilder();
        }

        public void End()
        {

        }

        public object GetResult()
        {
            return result;
        }

        private bool inLabel = false;
        private bool inAttribute = false;

        private char lastStartChar = '\0';
        private char lastChar = '\0';

        private string currentLabelName = null;
        private StringBuilder currentLabelNameBuilder = new StringBuilder();

        public void ProcessChar(char ch, int index)
        {
            if (ch == '<')
            {
                inLabel = true;
                currentLabelName = null;
                currentLabelNameBuilder.Clear();
                AddSpaceToResult();
                return;
            }
            else if (ch == '>')
            {
                inLabel = false;
                inAttribute = false;
                AddSpaceToResult();
                currentLabelName = null;
                currentLabelNameBuilder.Clear();
                return;
            }

            if (inLabel)
            {
                if ((ch == '\"' || ch == '\''))
                {
                    if (inAttribute && lastStartChar == ch)
                    {
                        inAttribute = false;
                        AddSpaceToResult();
                    }
                    else
                    {
                        inAttribute = true;
                        AddSpaceToResult();
                    }

                    lastStartChar = ch;
                }
                else if (inAttribute)
                {
                    if (Decision == Decision.Include && CompareLabelNames.Contains(currentLabelName))
                        AddToResult(ch);
                    else if (Decision == Decision.Exclude && !CompareLabelNames.Contains(currentLabelName))
                        AddToResult(ch);
                    // Console.WriteLine(currentLabelName);
                }
                else
                {
                    if (!char.IsLetter(ch))
                    {
                        currentLabelName = currentLabelNameBuilder.ToString().ToLower();
                    }
                    else if (currentLabelName == null)
                    {
                        currentLabelNameBuilder.Append(ch);
                    }
                }

            }
            else
            {
                AddToResult(ch);
            }
        }

        private void AddToResult(char ch)
        {
            if (specchars.Contains(ch) || ch == insertSplitChar)
            {
                ch = insertSplitChar;
                if (lastChar == insertSplitChar)
                {
                    return;
                }
            }

            result.Append(ch);
            lastChar = ch;
        }

        private void AddSpaceToResult()
        {
            if (lastChar == insertSplitChar)
                return;

            result.Append(insertSplitChar);
            lastChar = insertSplitChar;
        }
    }
}