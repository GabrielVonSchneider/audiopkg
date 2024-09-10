﻿using System.Text;

namespace audiopkg
{
    internal static class Util
    {
        public static ushort[] ReadUints16(BinaryReader reader, int count)
        {
            var ret = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                ret[i] = reader.ReadUInt16();
            }

            return ret;
        }

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

            Console.WriteLine($"index: {index}, null index: {nullIndex}");
            return Encoding.ASCII.GetString(buffer, index, nullIndex - index);
        }
    }
}