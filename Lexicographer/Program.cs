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
                var file = args[0];

                ModuleCompiler compiler = new ModuleCompiler();
                Path.GetDirectoryName(file);
                string withoutExtension = Path.GetFileNameWithoutExtension(file);
                compiler.Build((TextReader)reader, withoutExtension, Directory.GetCurrentDirectory());

                var e = compiler.Parser.Errors;
            }
        }
    }
}