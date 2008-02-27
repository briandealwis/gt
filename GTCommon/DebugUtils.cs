using System;
using System.IO;
using System.Text;

namespace GT.Common
{
    public class DebugUtils
    {
        public static void DumpMessage(string prefix, byte[] buffer)
        {
            int length = BitConverter.ToInt32(buffer, 4);
            byte[] data = new byte[length];
            Array.Copy(buffer, 8, data, 0, data.Length);
            DumpMessage(prefix, buffer[0], (MessageType)buffer[1], data);
        }

        public static void DumpMessage(string prefix, Message m)
        {
            DumpMessage(prefix, m.id, m.type, m.data);
        }

        public static void DumpMessage(string prefix, byte id, MessageType type, byte[] buffer)
        {
            if (prefix == null)
            {
                Write("  ");
            }
            else
            {
                Write(prefix);
                Write(" ");
            }
            switch (type)
            {
            case MessageType.String:
                WriteLine("String: '" + System.Text.ASCIIEncoding.ASCII.GetString(buffer) + "'");
                break;
            case MessageType.Binary:
                WriteLine("Binary: ");
                for (int i = 0; i < buffer.Length; i++)
                {
                    Write("    ");
                    int rem = Math.Min(16, buffer.Length - i);
                    for (int j = 0; j < 16; j++)
                    {
                        Write(' ');
                        Convert.ToString((int)buffer[i + j], 16);
                    }
                    i += rem;
                }
                break;

            case MessageType.System:
                WriteLine("System: " + (SystemMessageType)buffer[0]);
                break;
            }
        }

        public static TextWriter writer = new ConsoleWriter();

        public static void Write(char ch)
        {
            if (writer == null) { return; }
            writer.Write(ch);
        }

        public static void Write(string text)
        {
            if (writer == null) { return; }
            writer.Write(text);
        }

        protected static void WriteLine(string text)
        {
            if (writer == null) { return; }
            writer.WriteLine(text);
        }
    }

    internal class ConsoleWriter : TextWriter
    {
        override public void Write(string text)
        {
            Console.Write(text);
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
