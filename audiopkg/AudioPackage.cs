using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using static audiopkg.Util;

namespace audiopkg
{
    internal class AudioPackage
    {
        public string Version;
        public string Platform;
        public string User;
        public int nDescriptors;
        public int nIdentifiers;
        public int descriptorFootprint;
        public int stringTableFootprint;
        public int lipsyncTableFootprint;
        public int breakpointTableFootprint;
        public int musicDataFootprint;
        public int[] nSampleHeaders;
        public int[] nSampleIndices;
        public int[] compressionTypes;
        public int[] headerSizes;

        public List<DescriptorIdentifier> descriptorIdentifiers = new List<DescriptorIdentifier>();
        string[] stringTable;
        uint[] descriptorOffsets;
        Descriptor[] descriptors;
        ushort[] sampleHeaderIndices;
        List<SampleHeader> sampleHeaders;

        public const string platform_ps2 = "PlayStation II";
        public const string platform_pc = "Windows";
        const int numTemperatures = 3;


        public AudioPackage(BinaryReader reader)
        {
            Version = GetNullTerminatedString(reader.ReadBytes(16), 0);
            Platform = GetNullTerminatedString(reader.ReadBytes(16), 0);
            User = GetNullTerminatedString(reader.ReadBytes(16), 0);

            switch (Version)
            {
                case "v1.5":
                    reader.BaseStream.Seek(0xA0, SeekOrigin.Begin); //skip to the important stuff
                    break;
                case "v1.6":
                    reader.BaseStream.Seek(0xA0, SeekOrigin.Begin); //skip to the important stuff
                    break;
                default:
                    Debug.Fail($"unknown version {Version}");
                    break;
            }

            nDescriptors = reader.ReadInt32();
            nIdentifiers = reader.ReadInt32();
            descriptorFootprint = reader.ReadInt32();
            stringTableFootprint = reader.ReadInt32();
            lipsyncTableFootprint = reader.ReadInt32();
            breakpointTableFootprint = reader.ReadInt32();
            musicDataFootprint = reader.ReadInt32();
            nSampleHeaders = ReadInts32(reader, numTemperatures);
            nSampleIndices = ReadInts32(reader, numTemperatures);
            compressionTypes = ReadInts32(reader, numTemperatures);
            headerSizes = ReadInts32(reader, numTemperatures);
            Debug.Assert(headerSizes.Sum() == 120 && headerSizes[0] == 40, Dump(headerSizes));

            Console.WriteLine($"header counts: {nSampleHeaders[0]} {nSampleHeaders[1]} {nSampleHeaders[2]}");
            Console.WriteLine($"header index counts: {nSampleIndices[0]} {nSampleIndices[1]} {nSampleIndices[2]}");
            Console.WriteLine($"Descriptors: {nDescriptors}, Identifiers: {nIdentifiers}, descriptor footprint: {descriptorFootprint}");

            Console.WriteLine($"reading string table at 0x{reader.BaseStream.Position:x}");
            var stringTableBuffer = reader.ReadBytes(stringTableFootprint);

            //skip the non-relevant informatio
            Console.WriteLine($"Skipping music data of at: 0x{reader.BaseStream.Position:x}");
            reader.BaseStream.Seek(musicDataFootprint, SeekOrigin.Current);
            Console.WriteLine($"Skipping lipsync data at: 0x{reader.BaseStream.Position:x}");
            reader.BaseStream.Seek(lipsyncTableFootprint, SeekOrigin.Current);
            Console.WriteLine($"Skipping breakpoint data at: 0x{reader.BaseStream.Position:x}");
            reader.BaseStream.Seek(breakpointTableFootprint, SeekOrigin.Current);

            Console.WriteLine($"reading identifier table at: 0x{reader.BaseStream.Position:x}");
            for (int i = 0; i < nIdentifiers; i++)
            {
                descriptorIdentifiers.Add(DescriptorIdentifier.Read(reader));
                Console.WriteLine(descriptorIdentifiers[i]);
            }
            stringTable = new string[nIdentifiers];
            for (int i = 0; i < nIdentifiers; i++)
            {
                var id = descriptorIdentifiers[i];
                stringTable[id.Index] = GetNullTerminatedString(stringTableBuffer, id.StringOffset);
            }
            foreach (var entry in stringTable)
            {
                Console.WriteLine(entry);
            }

            Console.WriteLine($"reading descriptor offsets at: 0x{reader.BaseStream.Position:x}");
            descriptorOffsets = new uint[nDescriptors];
            for (int i = 0; i < nDescriptors; i++)
            {
                descriptorOffsets[i] = reader.ReadUInt32();
                Console.WriteLine(descriptorOffsets[i].ToString("x2"));
            }

            Console.WriteLine($"reading descriptors at: 0x{reader.BaseStream.Position:x}");
            var descriptorStart = reader.BaseStream.Position;
            var descriptorBuffer = reader.ReadBytes(descriptorFootprint);
            descriptors = new Descriptor[nDescriptors];
            for (int i = 0; i < nDescriptors; i++)
            {
                reader.BaseStream.Seek(descriptorOffsets[i] + descriptorStart, SeekOrigin.Begin);
                descriptors[i] = Descriptor.Read(reader);
            }
            reader.BaseStream.Seek(descriptorStart + descriptorFootprint, SeekOrigin.Begin);

            var totalSampleHeaderIndices = nSampleIndices.Sum(x => x == 0 ? 0 : x + 1); // why + 1?
            sampleHeaderIndices = new ushort[totalSampleHeaderIndices];
            Console.WriteLine($"reading sample header indices at: 0x{reader.BaseStream.Position:x}");
            for (int i = 0; i < totalSampleHeaderIndices; i++)
            {
                sampleHeaderIndices[i] = reader.ReadUInt16();
                Console.WriteLine(sampleHeaderIndices[i].ToString("x2"));
            }

            var totalSampleHeaders = nSampleHeaders.Sum();
            Console.WriteLine($"reading {totalSampleHeaders} sample headers at 0x{reader.BaseStream.Position:x}");
            sampleHeaders = new List<SampleHeader>();
            for (int i = 0; i < totalSampleHeaders; i++)
            {
                sampleHeaders.Add(SampleHeader.FromFile(reader));
                Console.WriteLine(sampleHeaders[i]);
            }
        }

        public void ExtractAllFiles(BinaryReader reader, Args args)
        {
            var outDir = Path.GetDirectoryName(args.Infile) ?? throw new InvalidOperationException();
            if (args.Vgmstream)
            {
                var txthPath = Path.Combine(outDir, ".vgmstream.txth");
                using var txtHeader = new StreamWriter(txthPath);

                switch (Platform)
                {
                    case platform_pc:
                        throw new InvalidOperationException("pc doesn't yet work with vgmstream");
                    case platform_ps2:
                        txtHeader.WriteLine($"codec = PSX");
                        txtHeader.WriteLine($"sample_rate = @0x{SampleHeader.SampleRateOffset:x}");
                        txtHeader.WriteLine($"channels = @0x{SampleHeader.VgmstreamChannelsOffset:x}");
                        txtHeader.WriteLine($"interleave = 0x8000");
                        txtHeader.WriteLine($"frame_size = 28");
                        txtHeader.WriteLine($"num_samples = @0x{SampleHeader.NSamplesOffset:x}");
                        txtHeader.WriteLine($"start_offset = 0x{SampleHeader.VgmstreamDataStart:x}");
                        break;
                }
            }

            Console.WriteLine($"extracting {nIdentifiers} identifiers");
            for (int i = 0; i < nIdentifiers; i++)
            {
                var id = descriptorIdentifiers[i];
                var descriptor = descriptors[id.Index];
                Console.WriteLine($"descriptor with {descriptor.Elements.Count} elements");
                for (int iEl = 0; iEl < descriptor.Elements.Count; iEl++)
                {
                    var element = descriptor.Elements[iEl];
                    string outFilePath = Path.Combine(outDir, stringTable[id.Index]);
                    if (descriptor.Elements.Count > 1)
                    {
                        outFilePath += $"_{i:d2}";
                    }

                    Debug.Assert(element.IndexType != index_type.DESCRIPTOR_INDEX);

                    //yes, this is actually how they did this
                    var channelCount = sampleHeaderIndices[element.Index + 1] - sampleHeaderIndices[element.Index];

                    if (args.Vgmstream)
                    {
                        outFilePath += ".vgmstream";
                        var header = sampleHeaders[sampleHeaderIndices[element.Index]];
                        using var outFile = File.OpenWrite(outFilePath);
                        using var writer = new BinaryWriter(outFile);

                        header.WriteToFile(writer);
                        writer.Write(channelCount);

                        reader.BaseStream.Seek(header.WaveformOffset, SeekOrigin.Begin);
                        var length = (int)(channelCount * header.WaveformLength);
                        var sample = reader.ReadBytes(length);
                        writer.BaseStream.Write(sample);
                    }
                    else
                    {
                        for (int iCh = 0; iCh < channelCount; iCh++)
                        {
                            var header = sampleHeaders[sampleHeaderIndices[element.Index] + iCh];
                            Console.WriteLine(header.ToString());
                            if (channelCount > 1)
                            {
                                string lr = iCh == 0 ? "left" : "right";
                                outFilePath += $"_{lr}";
                            }

                            if (args.Decompress)
                            {
                                outFilePath += ".wav";
                            }
                            else
                            {
                                outFilePath += ".raw";
                            }

                            byte[] sample;
                            if (channelCount > 1)
                            {
                                sample = ReadStereoHalf(iCh, reader.BaseStream, header);
                            }
                            else
                            {
                                reader.BaseStream.Seek(header.WaveformOffset, SeekOrigin.Begin);
                                sample = reader.ReadBytes((int)header.WaveformLength);
                            }

                            var outBytes = sample;
                            if (args.Decompress)
                            {
                                //possible todo: package the stereo halves into a stereo wav file
                                outBytes = Decompress(sample, header, args);
                            }

                            using var outFile = File.OpenWrite(outFilePath);
                            outFile.Write(outBytes, 0, outBytes.Length);
                        }
                    }
                }
            }
        }

        byte[] Decompress(byte[] sampleBytes, SampleHeader splHeader, Args arguments)
        {
            if (splHeader.CompressionType != 0) //adpcm
            {
                throw new InvalidOperationException($"unknown compression type {splHeader.CompressionType}");
            }

            if (Platform != platform_pc)
            {
                throw new InvalidOperationException($"adpcm can currently only be decompressed for pc versions.");
            }

            return Mss.DecompressAdpcm(sampleBytes, splHeader, this);
        }


        byte[] ReadStereoHalf(int leftRight, Stream file, SampleHeader header)
        {
            const int BufferSize = 36 * 1024;
            var outBytes = new byte[header.WaveformLength]; //todo: handle end of file padding correctly.
            for (int n = 0; BufferSize * (n + 1) < outBytes.Length; n++)
            {
                var offsetInFile = header.WaveformOffset + (n * BufferSize * 2) + (leftRight * BufferSize);
                file.Seek(offsetInFile, SeekOrigin.Begin);
                file.Read(outBytes, n * BufferSize, BufferSize);
            }
            return outBytes;
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

    class SampleHeader
    {
        public const int WaveformOffsetOffset = 0x04;
        public const int WaveformLengthOffset = 0x08;
        public const int NSamplesOffset = 0x18;
        public const int SampleRateOffset = 0x1C;

        public const int VgmstreamChannelsOffset = 0x28;
        public const int VgmstreamDataStart = 0x2C;

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

        public void WriteToFile(BinaryWriter writer, int? vgmstreamChannels = null)
        {
            writer.Write(AudioRam);
            writer.Write(WaveformOffset);
            writer.Write(WaveformLength);
            writer.Write(LipSyncOffset);
            writer.Write(BreakPointOffset);
            writer.Write(CompressionType);
            writer.Write(nSamples);
            writer.Write(SampleRate);
            writer.Write(LoopStart);
            writer.Write(LoopEnd);

            if (vgmstreamChannels != null)
            {
                writer.Write(vgmstreamChannels.Value);
            }
        }

        public override string ToString()
        {
            return $"Sample at 0x{WaveformOffset:x} with sample rate {SampleRate}, {nSamples} samples and length of {WaveformLength} and breakpoint offset {BreakPointOffset}";
        }
    }


}
