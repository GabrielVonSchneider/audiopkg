//an attempt at extracting the audiopkg format from hobbit 03

using audiopkg;
using System.Runtime.InteropServices;
using static audiopkg.AudioPackage;

if (!Args.TryParse(args, out var arguments))
{
    return 1;
}

using var infile = File.OpenRead(arguments.Infile);
using var reader = new BinaryReader(infile);
var package = new AudioPackage();
if (!package.TryLoad(reader, arguments))
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
    package.ExtractAllFiles(reader, arguments);
}
else
{
    Console.WriteLine($"read package with {package.descriptorIdentifiers.Count} identifiers");
}

return 0;


enum index_type
{
    HOT_INDEX = 0,          // Index references a loaded sample.
    WARM_INDEX = 1,         // Index references a "hybrid" sample.
    COLD_INDEX = 2,         // Index references a streamed sample.
    DESCRIPTOR_INDEX = 3,   // Index references an audio descriptor.
}

class Element
{
    public int Index;
    public index_type IndexType;
    public override string ToString()
    {
        return $"element with index type {this.IndexType} and index {this.Index}";
    }
}

class DescriptorIdentifier
{
    public ushort StringOffset;
    public ushort Index;
    public uint pPackage;

    public static DescriptorIdentifier Read(BinaryReader reader)
    {
        return new DescriptorIdentifier
        {
            StringOffset = reader.ReadUInt16(),
            Index = reader.ReadUInt16(),
            pPackage = reader.ReadUInt32(),
        };
    }

    public override string ToString()
    {
        return $"d.i. string offset = {StringOffset}, index = {Index}";
    }
}

[StructLayout(LayoutKind.Sequential)]
struct AILSOUNDINFO
{
    public int format;
    public IntPtr data_ptr;
    public uint data_len;
    public uint rate;
    public int bits;
    public int channels;
    public uint samples;
    public uint block_size;
}
