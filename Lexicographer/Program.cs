using Microsoft.SmallBasic;
using System.IO;

namespace TIKSN.SmallBasicWorkflow.Lexicographer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            using (var reader = File.OpenText(args[0]))
            {
                Parser parser = new Parser();
                parser.Parse(reader);

                var x = parser.SymbolTable;
            }
        }
    }
}