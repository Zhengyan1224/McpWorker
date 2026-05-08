// #define INDEV

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace Zhengyan.Commons
{
    public static class SystemUtils
    {
        public static void CompressLargeObjectHeap()
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
        }

        private static string programRoot = null;
        public static string ProgramRoot
        {
#if INDEV
            get
            {
                if (!string.IsNullOrWhiteSpace(programRoot))
                    return programRoot;
                
                programRoot = Path.GetFullPath("../");
                return programRoot;
            }
#else
            get
            {
                if (!string.IsNullOrWhiteSpace(programRoot))
                    return programRoot;
                string processFilePath = Process.GetCurrentProcess().MainModule.FileName;
                if (Path.GetFileNameWithoutExtension(processFilePath).ToLower() == "dotnet")
                    processFilePath = Assembly.GetEntryAssembly().Location;
                programRoot = Path.GetDirectoryName(processFilePath);
                return programRoot;
            }
#endif

            set
            {
                programRoot = value;
            }
        }

        public static string GetAbsolutePath(string relativePath)
        {
            var absolutePath = Path.Combine(ProgramRoot, relativePath);
            return Path.GetFullPath(absolutePath);
        }
    }
}
