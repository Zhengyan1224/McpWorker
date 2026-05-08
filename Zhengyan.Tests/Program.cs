using System;
using System.Text;
using System.Threading.Tasks;

namespace Zhengyan.Tests
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // await WebSearchTest.Run(args);
            // await WebSearcherMcpTest.Run(args);
            // await QueryCertInfoMcpTest.Run(args);
            // await SSHMcpTest.Run(args);
            // await SSHTest.Run(args);
            // await RegexTest.Run(args);
            // await LunarTest.Run(args);
            // await HNSWTest.Run(args);
            await HNSWV2Test.Run(args);
            // await DataStorageTest.Run(args);
        }
    }
}