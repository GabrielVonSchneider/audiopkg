//an attempt at extracting the audiopkg format from hobbit 03

using audiopkg;
using System.Runtime.InteropServices;
using static audiopkg.AudioPackage;

if (!Args.TryParse(args, out var arguments))
{
    return 1;
}

using var infile = File.OpenRead(arguments.Infile);
var package = new AudioPackage();
if (!package.TryLoad(infile, arguments))
{
    return 1;
}

if (arguments.Vgmstream && !new string[] { platform_ps2, platform_xbox }.Contains(package.Platform))
{
    Console.Error.WriteLine($"vgmstream only works with xbox and ps2.");
    return 1;
}

if (arguments.Decompress && package.Platform != platform_pc)
{
    Console.Error.WriteLine($"Only pc files can be decompressed at the moment.");
    return 1;
}

if (arguments.Extract)
{
    package.ExtractAllFiles(infile, arguments);
}
if (arguments.Print)
{
    package.WriteInfo();
}

return 0;
