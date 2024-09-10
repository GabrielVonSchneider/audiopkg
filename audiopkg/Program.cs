//an attempt at extracting the audiopkg format from hobbit 03

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using static audiopkg.Util;

var arglist = args.ToList();
var argLookup = new HashSet<string>();
for (int i = arglist.Count - 1; i >= 0; i--)
{
    if (arglist[i].StartsWith('-'))
    {
        argLookup.Add(arglist[i]);
        arglist.RemoveAt(i);
    }
}
if (arglist.Count != 1)
{
    Console.WriteLine("usage: audiopkg [-e] <filename>");
    return 1;
}
var infilePath = arglist[0];

const int numTemperatures = 3;

using var infile = File.OpenRead(infilePath);
using var reader = new BinaryReader(infile);

//read file header
var version = GetNullTerminatedString(reader.ReadBytes(16), 0);
var platform = GetNullTerminatedString(reader.ReadBytes(16), 0);
var user = GetNullTerminatedString(reader.ReadBytes(16), 0);

switch (version)
{
    case "v1.6":
        infile.Seek(0xA0, SeekOrigin.Begin); //skip to the important stuff
        break;
    case "v1.7":
        infile.Seek(0xC0, SeekOrigin.Begin); //skip to the important stuff
        break;
    default:
        Debug.Fail($"unknown version {version}");
        break;
}

var nDescriptors = reader.ReadInt32();
var nIdentifiers = reader.ReadInt32();
var descriptorFootprint = reader.ReadInt32();
var stringTableFootprint = reader.ReadInt32();
var lipsyncTableFootprint = reader.ReadInt32();
var breakpointTableFootprint = reader.ReadInt32();
var musicDataFootprint = reader.ReadInt32();
var nSampleHeaders = ReadInts32(reader, numTemperatures);
var nSampleIndices = ReadInts32(reader, numTemperatures);
var compressionTypes = ReadInts32(reader, numTemperatures);
var headerSizes = ReadInts32(reader, numTemperatures);
Debug.Assert(headerSizes.Sum() == 120 && headerSizes[0] == 40, Dump(headerSizes));

if (version == "v1.7")
{
    infile.Seek(4, SeekOrigin.Current); //some unknown bytes on area 51 xbox
}

Console.WriteLine($"header counts: {nSampleHeaders[0]} {nSampleHeaders[1]} {nSampleHeaders[2]}");
Console.WriteLine($"header index counts: {nSampleIndices[0]} {nSampleIndices[1]} {nSampleIndices[2]}");
Console.WriteLine($"Descriptors: {nDescriptors}, Identifiers: {nIdentifiers}, descriptor footprint: {descriptorFootprint}");

Console.WriteLine($"reading string table at 0x{infile.Position:x}");
var stringTableBuffer = reader.ReadBytes(stringTableFootprint);

//skip the non-relevant informatio
Console.WriteLine($"Skipping music data of at: 0x{infile.Position:x}");
infile.Seek(musicDataFootprint, SeekOrigin.Current);
Console.WriteLine($"Skipping lipsync data at: 0x{infile.Position:x}");
infile.Seek(lipsyncTableFootprint, SeekOrigin.Current);
Console.WriteLine($"Skipping breakpoint data at: 0x{infile.Position:x}");
infile.Seek(breakpointTableFootprint, SeekOrigin.Current);

Console.WriteLine($"reading identifier table at: 0x{infile.Position:x}");
var descriptorIdentifiers = new List<DescriptorIdentifier>();
for (int i = 0; i < nIdentifiers; i++)
{
    descriptorIdentifiers.Add(DescriptorIdentifier.Read(reader));
    Console.WriteLine(descriptorIdentifiers[i]);
}
var stringTable = new string[nIdentifiers];
for (int i = 0; i < nIdentifiers; i++)
{
    var id = descriptorIdentifiers[i];
    stringTable[id.Index] = GetNullTerminatedString(stringTableBuffer, id.StringOffset);
}
foreach (var entry in stringTable)
{
    Console.WriteLine(entry);
}

Console.WriteLine($"reading descriptor offsets at: 0x{infile.Position:x}");
var descriptorOffsets = new uint[nDescriptors];
for (int i = 0; i < nDescriptors; i++)
{
    descriptorOffsets[i] = reader.ReadUInt32();
    Console.WriteLine(descriptorOffsets[i].ToString("x2"));
}

Console.WriteLine($"reading descriptors at: 0x{infile.Position:x}");
var descriptorStart = infile.Position;
var descriptorBuffer = reader.ReadBytes(descriptorFootprint);
var descriptors = new Descriptor[nDescriptors];
for (int i = 0; i < nDescriptors; i++)
{
    infile.Seek(descriptorOffsets[i] + descriptorStart, SeekOrigin.Begin);
    descriptors[i] = Descriptor.Read(reader);
}
infile.Seek(descriptorStart + descriptorFootprint, SeekOrigin.Begin);

var totalSampleHeaderIndices = nSampleIndices.Sum(x => x == 0 ? 0 : x + 1); // why + 1?
var sampleHeaderIndices = new ushort[totalSampleHeaderIndices];
Console.WriteLine($"reading sample header indices at: 0x{infile.Position:x}");
for (int i = 0; i < totalSampleHeaderIndices; i++)
{
    sampleHeaderIndices[i] = reader.ReadUInt16();
    Console.WriteLine(sampleHeaderIndices[i].ToString("x2"));
}

var totalSampleHeaders = nSampleHeaders.Sum();
Console.WriteLine($"reading {totalSampleHeaders} sample headers at 0x{infile.Position:x}");
var sampleHeaders = new List<SampleHeader>();
for (int i = 0; i < totalSampleHeaders; i++)
{
    sampleHeaders.Add(SampleHeader.FromFile(reader));
    Console.WriteLine(sampleHeaders[i]);
}

var outDir = Path.GetDirectoryName(args[0]) ?? throw new InvalidOperationException();
if (argLookup.Contains("-e"))
{
    Console.WriteLine($"extracting {nIdentifiers} identifiers");
    for (int i = 0; i < nIdentifiers; i++)
    {
        var id = descriptorIdentifiers[i];
        var descriptor = descriptors[id.Index];
        Console.WriteLine($"descriptor with {descriptor.Elements.Count} elements");
        for (int iEl = 0; iEl < descriptor.Elements.Count; iEl++)
        {
            var element = descriptor.Elements[iEl];
            Debug.Assert(element.IndexType != index_type.DESCRIPTOR_INDEX);

            //yes, this is actually how they did this
            var channelCount = sampleHeaderIndices[element.Index + 1] - sampleHeaderIndices[element.Index];

            for (int iCh = 0; iCh < channelCount; iCh++) //just do this until we can split
            {
                var header = sampleHeaders[sampleHeaderIndices[element.Index] + iCh];
                Console.WriteLine(header.ToString());
                string outFilePath = Path.Combine(outDir, stringTable[id.Index]);
                if (descriptor.Elements.Count > 1)
                {
                    outFilePath += $"_{i:d2}";
                }
                if (channelCount > 1)
                {
                    string lr = iCh == 0 ? "left" : "right";
                    outFilePath += $"_{lr}";
                }

                outFilePath += ".wav";
                if (channelCount > 1)
                {
                    using var outFile = File.OpenWrite(outFilePath);
                    var sampleBytes = GetStereoHalf(iCh, infile, (int)header.WaveformOffset, (int)header.WaveformLength);
                    var wav = Mss.Decompress(sampleBytes, header, version);
                    outFile.Write(wav, 0, wav.Length);
                }
                else
                {
                    using var outFile = File.OpenWrite(outFilePath);
                    infile.Seek(header.WaveformOffset, SeekOrigin.Begin);
                    var sampleBytes = reader.ReadBytes((int)header.WaveformLength);
                    var wav = Mss.Decompress(sampleBytes, header, version);
                    outFile.Write(wav, 0, wav.Length);
                }
            }
        }
    }
}


byte[] GetStereoHalf(int leftRight, FileStream file, int sampleIndex, int sampleLength)
{
    const int BufferSize = 36 * 1024;
    var outBytes = new byte[sampleLength / 2]; //todo: handle end of file padding correctly.
    for (int n = 0; BufferSize * (n + 1) < outBytes.Length; n++)
    {
        var offsetInFile = sampleIndex + (n * BufferSize * 2) + (leftRight * BufferSize);
        try
        {
            file.Seek(offsetInFile, SeekOrigin.Begin);
        }
        catch
        {
            Console.WriteLine($"failed at {offsetInFile}");
            throw;
        }
        file.Read(outBytes, n * BufferSize, BufferSize);
    }
    return outBytes;
}

if (argLookup.Contains("--dump-headers"))
{
    foreach (var header in sampleHeaders)
    {
        Console.WriteLine(header);
    }
}
if (argLookup.Contains($"--extract-headers"))
{
    foreach (var header in sampleHeaders)
    {
        Console.WriteLine(header.ToString());
        var outFilePath = Path.Combine(outDir, header.WaveformOffset.ToString("x8") + ".wav");
        using var outFile = File.OpenWrite(outFilePath);
        infile.Seek(header.WaveformOffset, SeekOrigin.Begin);
        var sampleBytes = reader.ReadBytes((int)header.WaveformLength);
        var wav = Mss.Decompress(sampleBytes, header, version);
        outFile.Write(wav, 0, wav.Length);
    }
}

return 0;

int[] ReadInts32(BinaryReader reader, int count)
{
    var ret = new int[count];
    for (int i = 0; i < count; i++)
    {
        ret[i] = reader.ReadInt32();
    }

    return ret;
}

void Align(FileStream file, int byteCount) //might needthis later for writing.
{
    var mod = file.Position % byteCount;
    if (mod != 0)
    {
        file.Seek(byteCount - mod, SeekOrigin.Current);
    }
}

enum descriptor_type
{
    SIMPLE = 0,
    COMPLEX = 1,
    RANDOM_LIST = 2,
    WEIGHTED_LIST = 3,
    NUM_DESCRIPTOR_TYPES = 4,
}

class Descriptor
{
    public readonly List<Element> Elements = new List<Element>();

    static bool HasParams(uint index)
    {
        return (index & (1 << 13)) != 0;
    }

    public static Descriptor Read(BinaryReader reader)
    {
        var descriptorIndex = reader.ReadUInt32();
        var desc = new Descriptor();
        var type = (descriptor_type)(descriptorIndex >> 14 & 3); //type stored in bits 14-15
        bool hasParams = HasParams(descriptorIndex);
        if (hasParams)
        {
            //skip the params
            var parameterSize = reader.ReadUInt16() >> 1;
            reader.BaseStream.Seek((parameterSize - 1) * 2, SeekOrigin.Current); //parameter size is in words
        }

        if (type == descriptor_type.SIMPLE)
        {
            var el = new Element();
            var elementIndex = reader.ReadUInt32();
            el.IndexType = GetIndexType(elementIndex); //bits 14-15
            el.Index = GetIndex(elementIndex); //bits 0-12
            desc.Elements.Add(el);
        }
        else if (type == descriptor_type.WEIGHTED_LIST)
        {
            var elementCount = reader.ReadInt16();
            var weights = ReadUints16(reader, elementCount);
            for (int i = 0; i < elementCount; i++)
            {
                var elementIndex = reader.ReadUInt32();
                if (HasParams(elementIndex))
                {
                    var parameterSize = reader.ReadUInt16();
                    reader.BaseStream.Seek((parameterSize - 1) * 2, SeekOrigin.Current);
                }
                var el = new Element();
                el.Index = GetIndex(elementIndex);
                el.IndexType = GetIndexType(elementIndex);
                desc.Elements.Add(el);
            }
        }
        //lots of duplication here, but let's stick to the original source code right now
        else if (type == descriptor_type.RANDOM_LIST)
        {
            var elementCount = reader.ReadInt16();
            //don't really understand how this format works yet, but we can just skip "used bits"
            var usedBits = reader.ReadBytes(8); //4 words

            for (int i = 0; i < elementCount; i++)
            {
                var elementIndex = reader.ReadUInt32();
                if (HasParams(elementIndex))
                {
                    var parameterSize = reader.ReadUInt16();
                    Console.Write($"parameter size: {parameterSize}, ");
                    reader.BaseStream.Seek((parameterSize - 1) * 2, SeekOrigin.Current);
                }
                var el = new Element();
                el.Index = GetIndex(elementIndex);
                el.IndexType = GetIndexType(elementIndex);
                desc.Elements.Add(el);
            }
        }
        else
        {
            Debug.Fail($"unhandled descriptor type {type}");
        }
        return desc;
    }

    static index_type GetIndexType(uint elementIndex)
    {
        return (index_type)((elementIndex >> 14) & 3);
    }

    static int GetIndex(uint elementIndex)
    {
        return (int)(elementIndex & ((1 << 13) - 1));
    }
}

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

class SampleHeader
{
    public uint AudioRam;
    public uint WaveformOffset;
    public uint WaveformLength;
    public uint LipSyncOffset;
    public int BreakPointOffset;
    public uint CompressionType;
    public int nSamples;
    public int SampleRate;
    public int LoopStart;
    public int LoopEnd;

    public static SampleHeader FromFile(BinaryReader file)
    {
        return new SampleHeader
        {
            AudioRam = file.ReadUInt32(),
            WaveformOffset = file.ReadUInt32(),
            WaveformLength = file.ReadUInt32(),
            LipSyncOffset = file.ReadUInt32(),
            BreakPointOffset = file.ReadInt32(), //offset in the breakpoint table
            CompressionType = file.ReadUInt32(),
            nSamples = file.ReadInt32(),
            SampleRate = file.ReadInt32(),
            LoopStart = file.ReadInt32(),
            LoopEnd = file.ReadInt32(),
        };
    }

    public override string ToString()
    {
        return $"Sample at 0x{WaveformOffset:x} with sample rate {SampleRate}, {nSamples} samples and length of {WaveformLength} and breakpoint offset {BreakPointOffset}";
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


static class Mss
{
    public const int WAVE_FORMAT_IMA_ADPCM = 0x0011;
    [DllImport(@"Mss32.dll", SetLastError = true)]
    public static extern int AIL_decompress_ADPCM(ref AILSOUNDINFO soundInfo, out IntPtr wav, out int size);

    [DllImport(@"Mss32.dll", SetLastError = true)]
    public static extern IntPtr AIL_last_error();

    public static byte[] Decompress(byte[] sampleBytes, SampleHeader header, string version)
    {
        var adpcm_ptr = Marshal.AllocHGlobal(sampleBytes.Length);
        Marshal.Copy(sampleBytes, 0, adpcm_ptr, sampleBytes.Length);
        var info = new AILSOUNDINFO
        {
            format = WAVE_FORMAT_IMA_ADPCM,
            data_ptr = adpcm_ptr,
            data_len = (uint)sampleBytes.Length,
            rate = (uint)header.SampleRate,
            bits = 4,
            channels = 1, //todo: stereo files
            samples = (uint)header.nSamples,
            block_size = 36,
        };

        AIL_decompress_ADPCM(ref info, out var wavPointer, out var size);
        var output = new byte[size];
        Marshal.Copy(wavPointer, output, 0, size); //todo: call the appropriate function to free the memory.

        Marshal.FreeHGlobal(adpcm_ptr);
        return output;
    }
}
