//
// GT: The Groupware Toolkit for C#
// Copyright (C) 2006 - 2009 by the University of Saskatchewan
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later
// version.
// 
// This library is distributed in the hope that it will be useful, but
// WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA
// 02110-1301  USA
// 

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using GT.Net;

namespace GT.Utils
{
    public class DebugUtils
    {
        public static void DumpMessage(string prefix, byte[] buffer)
        {
            DumpMessage(prefix, buffer, 0, buffer.Length);
        }

        public static void DumpMessage(string prefix, Message m)
        {
            if (!Verbose) { return; }
            if (prefix == null)
            {
                Write("  ");
            }
            else
            {
                Write(prefix);
                Write(" ");
            }
            if(m is RawMessage) {
                WriteLine("Raw Message (uninterpreted bytes)");
                return;
            }
            switch (m.MessageType)
            {
            case MessageType.String:
                WriteLine("String: '" + ((StringMessage)m).Text + "'");
                break;

            case MessageType.Binary:
                WriteLine("Binary: ");
                byte[] buffer = ((BinaryMessage)m).Bytes;
                for (int i = 0; i < buffer.Length; i += 16)
                {
                    WriteLine("  {0}: {1}  {2}", i.ToString("X3"), 
                        ByteUtils.DumpBytes(buffer, i, 16),
                        ByteUtils.AsPrintable(buffer, i, 16));
                }
                break;

            case MessageType.Object:
                WriteLine("Object: " + ((ObjectMessage)m).Object);
                break;

            case MessageType.Session:
                WriteLine("Session: client " + ((SessionMessage)m).ClientId + " " + ((SessionMessage)m).Action);
                break;

            case MessageType.System:
                WriteLine("System: " + ((SystemMessage)m).Id);
                break;
            }
        }

        public static void DumpMessage(string prefix, byte[] buffer, int offset, int count)
        {
            if (!Verbose) { return; }
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
                Write("[" + ((SystemMessageType)buffer[offset]).ToString() + "]");
            } else {
                Write(" id=" + buffer[offset]);
            }
            WriteLine(" #bytes:" + count);
            Debug.Assert(count <= 500, "Message is awfully large: " + count + " bytes");
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

        public static void Write(string text, params object[] args)
        {
            if (writer == null) { return; }
            writer.Write(text, args);
        }

        public static void WriteLine(string text, params object[] args)
        {
            if (writer == null) { return; }
            writer.WriteLine(text, args);
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
