using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace GT.Net
{
    public class DebugUtils
    {
        public static void DumpMessage(string prefix, byte[] buffer)
        {
            DumpMessage(prefix, buffer, 0, buffer.Length);
        }

        public static void DumpMessage(string prefix, Message m)
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
            switch (m.type)
            {
            case MessageType.String:
                WriteLine("String: '" + ((StringMessage)m).text + "'");
                break;
            case MessageType.Binary:
                WriteLine("Binary: ");
                byte[] buffer = ((BinaryMessage)m).data;
                for (int i = 0; i < buffer.Length; i++)
                {
                    Write("    ");
                    int rem = Math.Min(16, buffer.Length - i);
                    Write(ByteUtils.DumpBytes(buffer, i, rem));
                    Write("   ");
                    Write(ByteUtils.AsPrintable(buffer, i, rem));
                    WriteLine("");
                    i += rem;
                }
                break;

            case MessageType.Object:
                WriteLine("Object: " + ((ObjectMessage)m).obj);
                break;

            case MessageType.Session:
                WriteLine("Session: client " + ((SessionMessage)m).ClientId + " " + ((SessionMessage)m).Action);
                break;

            case MessageType.System:
                WriteLine("System: " + ((SystemMessage)m).id);
                break;
            }
        }

        public static void DumpMessage(string prefix, byte[] buffer, int offset, int count)
        {
            if (prefix == null)
            {
                Write("  ");
            }
            else
            {
                Write(prefix);
                Write(": ");
            }
            Write("type=" + ((MessageType)buffer[offset + 1]).ToString());
            if (((MessageType)buffer[offset + 1]) == MessageType.System) {
                Write("{" + ((SystemMessageType)buffer[offset]).ToString() + "}");
            } else {
                Write(" id=" + buffer[offset]);
            }
            WriteLine(" #bytes:" + count);
            Debug.Assert(count <= 200);
            for (int i = offset; i < count; i++)
            {
                Write("    ");
                int rem = Math.Min(16, count - i);
                Write(ByteUtils.DumpBytes(buffer, i, rem)); 
                Write("   ");
                Write(ByteUtils.AsPrintable(buffer, i, rem));
                WriteLine("");
                i += rem;
            }
        }

        public static bool Verbose
        {
            get { return writer != null; }
            set
            {
                if (value)
                {
                    writer = new ConsoleWriter();
                }
                else
                {
                    writer = null;
                }
            }
        }

        public static TextWriter writer = null;
        //public static TextWriter writer = new ConsoleWriter();

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

        public static void WriteLine(string text)
        {
            if (writer == null) { return; }
            writer.WriteLine(text);
        }
    }

    internal class ConsoleWriter : TextWriter
    {

        override public void Write(char ch)
        {
            Console.Write(ch);
        }
        
        override public void Write(string text)
        {
            Console.Write(text);
        }

        override public void WriteLine(string text)
        {
            Console.WriteLine(text);
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }
}
