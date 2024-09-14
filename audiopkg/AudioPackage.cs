using System.Diagnostics;
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
        ushort[][] sampleHeaderIndices = new ushort[numTemperatures][];
        List<SampleHeader> hotSamples = new();
        List<SampleHeader> coldSamples = new();

        //should be safe to assume that there aren't different types of compression in the same package.
        int compressionType;

        public const string platform_ps2 = "PlayStation II";
        public const string platform_pc = "Windows";
        public const string platform_xbox = "Xbox";
        public const string platform_gamecube = "Gamecube";
        const int numTemperatures = 3;
        const int HOT = 0;
        const int WARM = 1;
        const int COLD = 2;


        public bool TryLoad(FileStream file, Args args)
        {
            var reader = new EndianAwareBinaryReader(file);
            Version = GetNullTerminatedString(reader.ReadBytes(16), 0);
            Platform = GetNullTerminatedString(reader.ReadBytes(16), 0);
            User = GetNullTerminatedString(reader.ReadBytes(16), 0);

            reader.IsBigEndian = Platform == platform_gamecube;

            switch (Version)
            {
                case "v1.5":
                    reader.BaseStream.Seek(0xA0, SeekOrigin.Begin);
                    break;
                case "v1.6":
                    if (Platform == platform_pc)
                    {
                        reader.BaseStream.Seek(0xA0, SeekOrigin.Begin);
                    }
                    else if (Platform == platform_xbox) //area 51 xbox demo
                    {
                        reader.BaseStream.Seek(0xB0, SeekOrigin.Begin);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unknown version {Version} for platfor {Platform}");
                    }
                    break;
                case "v1.7":
                    reader.BaseStream.Seek(0xC0, SeekOrigin.Begin);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown version {Version}");
                    return false;
            }

            nDescriptors = reader.ReadInt32();
            nIdentifiers = reader.ReadInt32();
            descriptorFootprint = reader.ReadInt32();
            stringTableFootprint = reader.ReadInt32();
            lipsyncTableFootprint = reader.ReadInt32();
            breakpointTableFootprint = reader.ReadInt32();
            musicDataFootprint = reader.ReadInt32();
            nSampleHeaders = reader.ReadInts32(numTemperatures);
            nSampleIndices = reader.ReadInts32(numTemperatures);
            args.WriteVerbose($"number of sample indices: {Dump(nSampleIndices)}");
            compressionTypes = reader.ReadInts32(numTemperatures);
            args.WriteVerbose($"compression types: {Dump(compressionTypes)}");
            headerSizes = reader.ReadInts32(numTemperatures);

            //assert that all the compression types are the same
            compressionType = -1;
            for (int i = 0; i < numTemperatures; i++)
            {
                if (nSampleHeaders[i] > 0)
                {
                    if (compressionType == -1)
                    {
                        compressionType = compressionTypes[i];
                    }
                    else
                    {
                        Debug.Assert(compressionTypes[i] == compressionType);
                    }

                    Debug.Assert(headerSizes[i] == 40);
                }
            }

            if ((Version == "v1.7" || Version == "v1.6") && Platform == platform_xbox)
            {
                reader.BaseStream.Seek(4, SeekOrigin.Current); //some unknown bytes on area 51 xbox.
            }

            args.WriteVerbose($"header counts: {nSampleHeaders[0]} {nSampleHeaders[1]} {nSampleHeaders[2]}");
            args.WriteVerbose($"header index counts: {nSampleIndices[0]} {nSampleIndices[1]} {nSampleIndices[2]}");
            args.WriteVerbose($"Descriptors: {nDescriptors}, Identifiers: {nIdentifiers}, descriptor footprint: {descriptorFootprint}");

            args.WriteVerbose($"reading string table at 0x{reader.BaseStream.Position:x}");
            var stringTableBuffer = reader.ReadBytes(stringTableFootprint);

            //skip the non-relevant informatio
            args.WriteVerbose($"Skipping music data of at: 0x{reader.BaseStream.Position:x}");
            reader.BaseStream.Seek(musicDataFootprint, SeekOrigin.Current);
            args.WriteVerbose($"Skipping lipsync data at: 0x{reader.BaseStream.Position:x}");
            reader.BaseStream.Seek(lipsyncTableFootprint, SeekOrigin.Current);
            args.WriteVerbose($"Skipping breakpoint data at: 0x{reader.BaseStream.Position:x}");
            reader.BaseStream.Seek(breakpointTableFootprint, SeekOrigin.Current);

            args.WriteVerbose($"reading identifier table at: 0x{reader.BaseStream.Position:x}");
            for (int i = 0; i < nIdentifiers; i++)
            {
                descriptorIdentifiers.Add(DescriptorIdentifier.Read(reader));
                args.WriteVerbose(descriptorIdentifiers[i].ToString());
            }
            stringTable = new string[nIdentifiers];
            for (int i = 0; i < nIdentifiers; i++)
            {
                var id = descriptorIdentifiers[i];
                stringTable[id.Index] = GetNullTerminatedString(stringTableBuffer, id.StringOffset);
            }
            foreach (var entry in stringTable)
            {
                args.WriteVerbose(entry);
            }

            args.WriteVerbose($"reading descriptor offsets at: 0x{reader.BaseStream.Position:x}");
            descriptorOffsets = new uint[nDescriptors];
            for (int i = 0; i < nDescriptors; i++)
            {
                descriptorOffsets[i] = reader.ReadUInt32();
                args.WriteVerbose(descriptorOffsets[i].ToString("x2"));
            }

            args.WriteVerbose($"reading descriptors at: 0x{reader.BaseStream.Position:x}");
            var descriptorStart = reader.BaseStream.Position;
            var descriptorBuffer = reader.ReadBytes(descriptorFootprint);
            descriptors = new Descriptor[nDescriptors];
            for (int i = 0; i < nDescriptors; i++)
            {
                reader.BaseStream.Seek(descriptorOffsets[i] + descriptorStart, SeekOrigin.Begin);
                descriptors[i] = Descriptor.Read(reader, args);
            }
            reader.BaseStream.Seek(descriptorStart + descriptorFootprint, SeekOrigin.Begin);

            args.WriteVerbose($"reading sample header indices at: 0x{reader.BaseStream.Position:x}");
            for (int temp = 0; temp < numTemperatures; temp++)
            {
                if (nSampleIndices[temp] == 0)
                {
                    continue;
                }

                //+1 for the stereo calculation
                sampleHeaderIndices[temp] = new ushort[nSampleIndices[temp] + 1];
                for (int i = 0; i < sampleHeaderIndices[temp].Length; i++)
                {
                    sampleHeaderIndices[temp][i] = reader.ReadUInt16();
                    args.WriteVerbose(sampleHeaderIndices[temp][i].ToString("x2"));
                }
            }

            args.WriteVerbose($"reading {nSampleHeaders[HOT]} hot sample headers at 0x{reader.BaseStream.Position:x}");
            for (int i = 0; i < nSampleHeaders[HOT]; i++)
            {
                hotSamples.Add(SampleHeader.FromFile(reader));
                args.WriteVerbose(hotSamples[i].ToString());
            }
            args.WriteVerbose($"reading {nSampleHeaders[COLD]} cold sample headers at 0x{reader.BaseStream.Position:x}");
            for (int i = 0; i < nSampleHeaders[COLD]; i++)
            {
                coldSamples.Add(SampleHeader.FromFile(reader));
                args.WriteVerbose(coldSamples[i].ToString());
            }

            return true;
        }

        public void ExtractAllFiles(Stream stream, Args args)
        {
            var reader = new EndianAwareBinaryReader(stream) { IsBigEndian = Platform == platform_gamecube, };
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
                    case platform_xbox:
                        txtHeader.WriteLine($"codec = XBOX");
                        txtHeader.WriteLine($"sample_rate = @0x{SampleHeader.SampleRateOffset:x}");
                        txtHeader.WriteLine($"channels = 1");
                        txtHeader.WriteLine($"frame_size = 64");
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
                args.WriteVerbose($"descriptor with {descriptor.Elements.Count} elements");
                for (int iEl = 0; iEl < descriptor.Elements.Count; iEl++)
                {
                    var element = descriptor.Elements[iEl];
                    string outFileStart = Path.Combine(outDir, stringTable[id.Index]);
                    if (descriptor.Elements.Count > 1)
                    {
                        outFileStart += $"_{iEl:d2}";
                    }

                    var indices = element.IndexType switch
                    {
                        index_type.HOT_INDEX => sampleHeaderIndices[HOT],
                        index_type.COLD_INDEX => sampleHeaderIndices[COLD],
                        _ => throw new InvalidOperationException($"Unsupported index type {element.IndexType}"),
                    };

                    var headers = element.IndexType switch
                    {
                        index_type.HOT_INDEX => hotSamples,
                        index_type.COLD_INDEX => coldSamples,
                        _ => throw new InvalidOperationException($"Unsupported index type {element.IndexType}"),
                    };

                    if (element.Index + 1 >= indices.Length)
                    {
                        Console.Error.WriteLine($"Warning: out of range descriptor index 0x{element.Index:x} for identifier {stringTable[id.Index]} - skipping.");
                        continue;
                    }

                    //yes, this is actually how they did this
                    var nChannels = indices[element.Index + 1] - indices[element.Index];
                    Debug.Assert(nChannels <= 2);

                    bool interleaved = true;
                    if (nChannels == 2 && headers[indices[element.Index] + 1].WaveformOffset != headers[indices[element.Index]].WaveformOffset)
                    {
                        //this is true for some of the files in the xbox demo:
                        //l and r aren't interleaved, but just written separately
                        interleaved = false;
                    }

                    if (compressionType == (uint)CompressionType.Mp3
                        || (compressionType == (uint)CompressionType.Adpcm && Platform == platform_ps2))
                    {
                        string outFilePath = outFileStart;
                        var header = headers[indices[element.Index]];
                        args.WriteVerbose(header.ToString());

                        if (args.Decompress)
                        {
                            outFilePath += ".wav";
                        }
                        else if (args.Vgmstream)
                        {
                            outFilePath += ".vgmstream";
                        }
                        else if (header.CompressionType == CompressionType.Mp3)
                        {
                            outFilePath += ".mp3";
                        }
                        else
                        {
                            outFilePath += ".raw";
                        }

                        var sample = ReadSample(reader.BaseStream, header, nChannels, 0);
                        using var outFile = File.OpenWrite(outFilePath);
                        using var binWriter = new BinaryWriter(outFile);
                        if (args.Vgmstream)
                        {
                            header.WriteToFile(binWriter);
                            binWriter.Write(nChannels);
                        }
                        outFile.Write(sample, 0, sample.Length);
                    }
                    else
                    {
                        for (int iCh = 0; iCh < nChannels; iCh++)
                        {
                            string outFilePath = outFileStart;
                            var header = headers[indices[element.Index] + iCh];
                            args.WriteVerbose(header.ToString());
                            if (nChannels > 1)
                            {
                                string lr = iCh == 0 ? "left" : "right";
                                outFilePath += $"_{lr}";
                            }

                            if (args.Decompress)
                            {
                                outFilePath += ".wav";
                            }
                            else if (args.Vgmstream)
                            {
                                outFilePath += ".vgmstream";
                            }
                            else
                            {
                                outFilePath += ".raw";
                            }

                            byte[] sample;
                            if (interleaved)
                            {
                                sample = ReadSample(reader.BaseStream, header, nChannels, iCh);
                            }
                            else
                            {
                                sample = ReadSample(reader.BaseStream, header, 1, 0);
                            }

                            if (args.Decompress)
                            {
                                //possible todo: package the stereo halves into a stereo wav file
                                sample = Decompress(sample, header, args);
                            }

                            using var outFile = File.OpenWrite(outFilePath);
                            using var binWriter = new BinaryWriter(outFile);
                            if (args.Vgmstream)
                            {
                                header.WriteToFile(binWriter);
                                binWriter.Write(nChannels);
                            }
                            outFile.Write(sample, 0, sample.Length);
                        }
                    }

                    //todo: write txtp files for combining the left and right
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

        byte[] ReadSample(Stream file, SampleHeader header, int nChannels, int leftRight)
        {
            return Platform switch
            {
                platform_xbox => ReadXbox(file, header, nChannels, leftRight),
                platform_pc => ReadPC(file, header, nChannels, leftRight),
                platform_ps2 => ReadPlain(file, header),
                platform_gamecube => ReadPlain(file, header),
                _ => throw new InvalidOperationException($"unhandled platform {Platform}"),
            };
        }

        byte[] ReadPlain(Stream file, SampleHeader header)
        {
            var outBuffer = new byte[header.WaveformLength];
            file.Seek(header.WaveformOffset, SeekOrigin.Begin);
            file.Read(outBuffer);
            return outBuffer;
        }

        //untangle the xbox interleave
        byte[] ReadXbox(Stream file, SampleHeader header, int nChannels, int leftRight)
        {
            if (nChannels == 2)
            {
                Debug.Assert(header.WaveformLength % 2 == 0);
            }

            var outBytes = new byte[header.WaveformLength / nChannels];
            const int BufferSize = 32 * 1024;
            //this calculation is directly from ProcessSample.cpp - 16 bytes of waste at the end of every other block
            var waste = (BufferSize * 2) % 36;
            int[] dataSizes = [BufferSize, BufferSize - waste];
            int[] dataWaste = [0, waste];
            int totalBytesRead = 0;
            file.Seek(header.WaveformOffset, SeekOrigin.Begin);
            file.Seek(BufferSize * leftRight, SeekOrigin.Current);
            for (int evenOdd = 0; ; evenOdd ^= 1)
            {
                var toRead = Math.Min(dataSizes[evenOdd], outBytes.Length - totalBytesRead); //todo: calculate correctly for stereo
                if (toRead == 0 || toRead + totalBytesRead > outBytes.Length)
                {
                    break;
                }

                var bytesRead = file.Read(outBytes, totalBytesRead, toRead);
                if (bytesRead != toRead)
                {
                    Console.Error.WriteLine($"warning: read past eof. channels: {nChannels} l/r: {leftRight}");
                    break;
                }
                totalBytesRead += bytesRead;
                file.Seek(dataWaste[evenOdd], SeekOrigin.Current);
                file.Seek(BufferSize * (nChannels - 1), SeekOrigin.Current);
            }

            return outBytes;
        }

        byte[] ReadPC(Stream file, SampleHeader header, int nChannels, int leftRight)
        {
            if (nChannels == 1)
            {
                return ReadPlain(file, header);
            }

            return ReadStereoHalfPc(file, header, leftRight);
        }

        byte[] ReadStereoHalfPc(Stream file, SampleHeader header, int leftRight)
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

    class DescriptorIdentifier
    {
        public ushort StringOffset;
        public ushort Index;
        public uint pPackage;

        public static DescriptorIdentifier Read(EndianAwareBinaryReader reader)
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

    class Element
    {
        public int Index;
        public index_type IndexType;
        public override string ToString()
        {
            return $"element with index type {this.IndexType} and index 0x{this.Index:x}";
        }
    }

    class Descriptor
    {
        public readonly List<Element> Elements = new List<Element>();

        static bool HasParams(ushort index)
        {
            return (index & (1 << 13)) != 0;
        }

        public static Descriptor Read(EndianAwareBinaryReader reader, Args arguments)
        {
            var descriptorIndex = reader.ReadUInt16();
            reader.BaseStream.Seek(2, SeekOrigin.Current); //skip some flags
            var desc = new Descriptor();
            var type = (descriptor_type)(descriptorIndex >> 14 & 3); //type stored in bits 14-15
            bool hasParams = HasParams(descriptorIndex);
            if (hasParams)
            {
                //skip the params
                var parameterSize = reader.ReadUInt16() >> 1;
                //parameter size is in words and includes the parameter size value
                reader.BaseStream.Seek((parameterSize - 1) * 2, SeekOrigin.Current);
            }

            arguments.WriteVerbose($"reading descriptor of type {type} at 0x{reader.BaseStream.Position:x}");
            if (type == descriptor_type.SIMPLE)
            {
                var el = new Element();
                var elementIndex = reader.ReadUInt16();
                reader.BaseStream.Seek(2, SeekOrigin.Current); //skip over some parameters
                el.IndexType = GetIndexType(elementIndex); //bits 14-15
                el.Index = GetIndex(elementIndex); //bits 0-12
                desc.Elements.Add(el);
            }
            else if (type == descriptor_type.COMPLEX)
            {
                var elementCount = reader.ReadInt16();
                for (int i = 0; i < elementCount; i++)
                {
                    var deltaTime = reader.ReadInt16(); //ignore for now
                    var elementIndex = reader.ReadUInt16();
                    if (HasParams(elementIndex)) //skip over parameters 
                    {
                        var parameterSize = reader.ReadUInt16();
                        reader.BaseStream.Seek((parameterSize - 1) * 2, SeekOrigin.Current);
                    }
                    var el = new Element();
                    el.IndexType = GetIndexType(elementIndex); //bits 14-15
                    el.Index = GetIndex(elementIndex); //bits 0-12
                    desc.Elements.Add(el);
                }
            }
            else if (type == descriptor_type.WEIGHTED_LIST)
            {
                var elementCount = reader.ReadInt16();
                var weights = reader.ReadUints16(elementCount);
                for (int i = 0; i < elementCount; i++)
                {
                    var elementIndex = reader.ReadUInt16();
                    reader.BaseStream.Seek(2, SeekOrigin.Current); //skip over some parameters
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
                    var elementIndex = reader.ReadUInt16();
                    reader.BaseStream.Seek(2, SeekOrigin.Current); //skip over some parameters
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
            else
            {
                Debug.Fail($"unhandled descriptor type {type}");
            }

            foreach (var element in desc.Elements)
            {
                arguments.WriteVerbose($"element with index 0x{element.Index:x2} and type {element.IndexType}");
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

    enum CompressionType : uint
    {
        Adpcm = 0,
        Pcm = 1,
        Mp3 = 2,
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
        public CompressionType CompressionType;
        public int nSamples;
        public int SampleRate;
        public int LoopStart;
        public int LoopEnd;

        public static SampleHeader FromFile(EndianAwareBinaryReader file)
        {
            return new SampleHeader
            {
                AudioRam = file.ReadUInt32(),
                WaveformOffset = file.ReadUInt32(),
                WaveformLength = file.ReadUInt32(),
                LipSyncOffset = file.ReadUInt32(),
                BreakPointOffset = file.ReadInt32(), //offset in the breakpoint table
                CompressionType = (CompressionType)file.ReadUInt32(),
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
            writer.Write((uint)CompressionType);
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
            return $"Sample at 0x{WaveformOffset:x} with sample rate {SampleRate}, 0x{nSamples:x} samples and length of 0x{WaveformLength:x} and compression type {CompressionType}";
        }
    }
}
