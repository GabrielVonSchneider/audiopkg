using System.Runtime.InteropServices;

namespace audiopkg
{
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

        public static byte[] DecompressAdpcm(ArraySegment<byte> sampleBytes, SampleHeader header)
        {
            var adpcm_ptr = Marshal.AllocHGlobal(sampleBytes.Count);
            Marshal.Copy(sampleBytes.Array ?? throw new InvalidOperationException(), sampleBytes.Offset, adpcm_ptr, sampleBytes.Count);
            var info = new AILSOUNDINFO
            {
                format = WAVE_FORMAT_IMA_ADPCM,
                data_ptr = adpcm_ptr,
                data_len = (uint)sampleBytes.Count,
                rate = (uint)header.SampleRate,
                bits = 4,
                channels = 1, //for adpcm the stereo channels are each encoded as mono then interleaved manually
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
}