using System.Text;

namespace audiopkg
{
    internal static class Util
    {
        public static string Dump(byte[] data)
        {
            return string.Join(", ", data.Select(x => x.ToString("x2")));
        }

        public static string Dump(int[] data)
        {
            return string.Join(", ", data.Select(x => x.ToString("x8")));
        }

        public static string GetNullTerminatedString(byte[] buffer, int index)
        {
            int nullIndex;
            for (nullIndex = index; nullIndex < buffer.Length; nullIndex++)
            {
                if (buffer[nullIndex] == 0)
                {
                    break;
                }
            }

            return Encoding.ASCII.GetString(buffer, index, nullIndex - index);
        }

        public static void Align(FileStream file, int byteCount) //might need this later for writing.
        {
            var mod = file.Position % byteCount;
            if (mod != 0)
            {
                file.Seek(byteCount - mod, SeekOrigin.Current);
            }
        }
    }
}
