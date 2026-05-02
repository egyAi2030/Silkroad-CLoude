using System;
using System.IO;
using System.Text;

namespace SilkroadAIBot.Domain.Network
{
    /// <summary>
    /// Thread-safe reader for <see cref="SRPacket"/> payloads.
    /// Implements standard Silkroad data types (Ascii, Unicode, UInt32, etc.).
    /// </summary>
    public sealed class SRPacketReader : IDisposable
    {
        private readonly MemoryStream _ms;
        private readonly BinaryReader _reader;

        public SRPacketReader(SRPacket packet)
        {
            _ms = new MemoryStream(packet.Payload, false);
            _reader = new BinaryReader(_ms, Encoding.ASCII);
        }

        public byte ReadByte() => _reader.ReadByte();
        public bool ReadBool() => _reader.ReadByte() != 0;
        public ushort ReadUInt16() => _reader.ReadUInt16();
        public short ReadInt16() => _reader.ReadInt16();
        public uint ReadUInt32() => _reader.ReadUInt32();
        public int ReadInt32() => _reader.ReadInt32();
        public ulong ReadUInt64() => _reader.ReadUInt64();
        public float ReadSingle() => _reader.ReadSingle();
        public double ReadDouble() => _reader.ReadDouble();

        public uint PeekUInt32()
        {
            long pos = _ms.Position;
            uint val = _reader.ReadUInt32();
            _ms.Position = pos;
            return val;
        }

        /// <summary>Reads a length-prefixed ASCII string.</summary>
        public string ReadAscii()
        {
            ushort length = ReadUInt16();
            if (length == 0) return string.Empty;
            byte[] bytes = _reader.ReadBytes(length);
            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>Reads a length-prefixed Unicode (UTF-16) string.</summary>
        public string ReadUnicode()
        {
            ushort length = ReadUInt16();
            if (length == 0) return string.Empty;
            byte[] bytes = _reader.ReadBytes(length * 2);
            return Encoding.Unicode.GetString(bytes);
        }

        public byte[] ReadBytes(int count) => _reader.ReadBytes(count);

        public long Position => _ms.Position;

        public long Remaining => _ms.Length - _ms.Position;

        public void Dispose()
        {
            _reader.Dispose();
            _ms.Dispose();
        }
    }

    /// <summary>
    /// Builder for <see cref="SRPacket"/> payloads.
    /// </summary>
    public sealed class SRPacketWriter : IDisposable
    {
        private readonly MemoryStream _ms;
        private readonly BinaryWriter _writer;
        private readonly ushort _opcode;
        private readonly bool _encrypted;

        public SRPacketWriter(ushort opcode, bool encrypted = false)
        {
            _opcode = opcode;
            _encrypted = encrypted;
            _ms = new MemoryStream();
            _writer = new BinaryWriter(_ms, Encoding.ASCII);
        }

        public void WriteByte(byte val) => _writer.Write(val);
        public void WriteBool(bool val) => _writer.Write((byte)(val ? 1 : 0));
        public void WriteUInt16(ushort val) => _writer.Write(val);
        public void WriteInt16(short val) => _writer.Write(val);
        public void WriteUInt32(uint val) => _writer.Write(val);
        public void WriteInt32(int val) => _writer.Write(val);
        public void WriteUInt64(ulong val) => _writer.Write(val);
        public void WriteSingle(float val) => _writer.Write(val);
        public void WriteDouble(double val) => _writer.Write(val);

        public void WriteAscii(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                WriteUInt16(0);
                return;
            }
            byte[] bytes = Encoding.ASCII.GetBytes(val);
            WriteUInt16((ushort)bytes.Length);
            _writer.Write(bytes);
        }

        public void WriteUnicode(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                WriteUInt16(0);
                return;
            }
            byte[] bytes = Encoding.Unicode.GetBytes(val);
            WriteUInt16((ushort)(bytes.Length / 2));
            _writer.Write(bytes);
        }

        public void WriteBytes(byte[] val) => _writer.Write(val);

        public SRPacket Build()
        {
            return new SRPacket(_opcode, _ms.ToArray(), _encrypted);
        }

        public void Dispose()
        {
            _writer.Dispose();
            _ms.Dispose();
        }
    }
}
