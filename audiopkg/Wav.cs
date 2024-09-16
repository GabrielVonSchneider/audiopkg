using System.Diagnostics;
using System.Text;

namespace audiopkg
{
    internal class Wav
    {
        public static void WriteWavFile(string baseName, ArraySegment<byte>[] channelData, SampleHeader header)
        {
            Debug.Assert(channelData[0].Count % 2 == 0);
            if (channelData.Length == 2)
            {
                Debug.Assert(channelData[0].Count == channelData[1].Count);
            }

            var fullName = $"{baseName}.wav";
            using var outFile = File.OpenWrite(fullName);
            var binWriter = new BinaryWriter(outFile);
            binWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
            binWriter.Write((channelData[0].Count * channelData.Length) + 44); //file length
            binWriter.Write(Encoding.ASCII.GetBytes("WAVE"));
            binWriter.Write(Encoding.ASCII.GetBytes("fmt "));
            binWriter.Write(16); //chunk size
            binWriter.Write((short)1); //pcm
            binWriter.Write((short)channelData.Length); //number of channels
            binWriter.Write(header.SampleRate);
            binWriter.Write(header.SampleRate * 2 * channelData.Length); //bytes per second
            binWriter.Write((short)(2 * channelData.Length)); //bytes per sample (block align)
            binWriter.Write((short)16); //bits per sample
            binWriter.Write(Encoding.ASCII.GetBytes("data"));
            binWriter.Write(channelData[0].Count * channelData.Length); //data length
            for (int i = 0; i < channelData[0].Count; i += 2)
            {
                for (int iCh = 0; iCh < channelData.Length; iCh++)
                {
                    binWriter.Write(channelData[iCh][i]);
                    binWriter.Write(channelData[iCh][i + 1]);
                }
            }
        }
    }
}
