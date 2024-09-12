namespace audiopkg
{
    internal class Args
    {
        public string Infile;
        public bool Decompress;
        public bool Extract;
        public bool Vgmstream;
        public bool Verbose;

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
                        case "txth":
                            outArgs.Vgmstream = true;
                            break;
                        case "verbose":
                            outArgs.Verbose = true;
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
                        case "t":
                            outArgs.Vgmstream = true;
                            break;
                        case "v":
                            outArgs.Verbose = true;
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
    -t, --txth: package the extracted audio for vgmstream using a txth file
    -v, --verbose: print a bunch of information as we're reading the file
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

        public void WriteVerbose(string text)
        {
            if (Verbose)
            {
                Console.WriteLine(text);
            }
        }
    }
}
