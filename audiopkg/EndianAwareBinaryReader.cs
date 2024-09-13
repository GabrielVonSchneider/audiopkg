using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace audiopkg
{
    internal class EndianAwareBinaryReader
    {
        public readonly Stream BaseStream;
        public bool IsBigEndian;

        public EndianAwareBinaryReader(Stream stream)
        {
            this.BaseStream = stream;
        }

        byte[] ReadAwareBytes(int count)
        {
            var bytes = new byte[count];
            BaseStream.Read(bytes);
            if (IsBigEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        public byte[] ReadBytes(int count)
        {
            var bytes = new byte[count];
            BaseStream.Read(bytes);
            return bytes;
        }

        public int ReadInt32() => BitConverter.ToInt32(ReadAwareBytes(4));
        public uint ReadUInt32() => BitConverter.ToUInt32(ReadAwareBytes(4));
        public short ReadInt16() => BitConverter.ToInt16(ReadAwareBytes(2));
        public ushort ReadUInt16() => BitConverter.ToUInt16(ReadAwareBytes(2));

        public int[] ReadInts32(int count)
        {
            var ret = new int[count];
            for (int i = 0; i < count; i++)
            {
                ret[i] = ReadInt32();
            }

            return ret;
        }

        public ushort[] ReadUints16(int count)
        {
            var ret = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                ret[i] = ReadUInt16();
            }

            return ret;
        }
    }
}
