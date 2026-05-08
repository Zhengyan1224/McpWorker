using System.Text.RegularExpressions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Zhengyan.WebSearch;

public class RegexTest
{
    public static async Task Run(string[] args)
    {
        var regex = new Regex(@"^\s*ls( .*)?");

        while(true)
        {
            Console.Write("Enter a command: ");
            string input = Console.ReadLine();
            
            var match = regex.Match(input);
            if (match.Success)
            {
                Console.WriteLine($"Matched: {match.Value}");
                Console.WriteLine($"Group 1: {match.Groups[1].Value}");
            }
            else
            {
                Console.WriteLine("No match found.");
            }
        }
    }
}