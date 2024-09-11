using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace audiopkg
{
    internal class Args
    {
        public string Infile;
        public bool Decompress;
        public bool Extract;
        public bool Vgmstream;

        public static bool TryParse(string[] args, out Args outArgs)
        {
            var arglist = args.ToList();
            outArgs = new Args();
            for (int i = arglist.Count - 1; i >= 0; i--)
            {
                if (arglist[i].StartsWith("--"))
                {
                    if (arglist[i].Length == 2)
                    {
                        Console.Error.WriteLine("nameless argument");
                        return false;
                    }

                    switch (arglist[i].Substring(2))
                    {
                        case "extract":
                            outArgs.Extract = true;
                            break;
                        case "decompress":
                            outArgs.Decompress = true;
                            break;
                        case "vgmstream":
                            outArgs.Vgmstream = true;
                            break;
                    }

                    arglist.RemoveAt(i);
                }
                else if (arglist[i].StartsWith("-"))
                {
                    if (arglist[i].Length == 1)
                    {
                        Console.Error.WriteLine("nameless argument");
                        return false;
                    }

                    switch (arglist[i].Substring(1))
                    {
                        case "e":
                            outArgs.Extract = true;
                            break;
                        case "d":
                            outArgs.Decompress = true;
                            break;
                        case "v":
                            outArgs.Vgmstream = true;
                            break;
                    }

                    arglist.RemoveAt(i);
                }
            }

            if (arglist.Count != 1)
            {
                Console.Write(@"usage: audiopkg <filename> [args]
flags:
    -e, --extract: extract all audio files from the package
    -d, --decompress: decompress all audio files
    -v, --vgmstream: package the extracted audio for vgmstream
");
                return false;
            }

            if (outArgs.Decompress && outArgs.Vgmstream)
            {
                Console.Error.WriteLine($"decompress and txth options are mutually exclusive.");
            }

            outArgs.Infile = arglist[0];
            return true;
        }
    }
}
