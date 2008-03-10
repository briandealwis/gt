using System;
using System.Collections.Generic;
using System.Text;
using Compression;
using GT.Net;

namespace GMCCompression
{
    public class GMCCompression
    {
        public int UniqueIdentity
        {
            get
            {
                return stream.UniqueIdentity;
            }
        }

        private enum GMCMessageType
        {
            Announcements = 1,
            Frequencies = 2,
            Templates = 3,
            String = 4
        }
        private GeneralMessageCompressor gmc;
        private BinaryStream stream;

        public GMCCompression(BinaryStream stream)
        {
            gmc = new GeneralMessageCompressor();
            gmc.SendAnnouncementEvent += new GeneralMessageCompressor.Send(gmc_SendAnnouncementEvent);
            gmc.SendFrequenciesEvent += new GeneralMessageCompressor.Send(gmc_SendFrequenciesEvent);
            gmc.SendTemplateEvent += new GeneralMessageCompressor.Send(gmc_SendTemplateEvent);
            this.stream = stream;
        }

        public String DequeueMessage()
        {
            byte[] b,c;
            while ((b = stream.DequeueMessage(0)) != null)
            {
                c = new byte[b.Length -1];
                Array.Copy(b,1,c,0,c.Length);
                switch(b[0])
                {
                    case (int)GMCMessageType.Announcements:
                        gmc.RecieveAnnouncements(c);
                        break;
                    case (int)GMCMessageType.Frequencies:
                        gmc.RecieveFrequencies(c);
                        break;
                    case (int)GMCMessageType.Templates:
                        gmc.ReceiveTemplates(c);
                        break;
                    default:
                        return gmc.DecodeString(c);
                }
            }
            return null;
        }

        public void Send(String s)
        {
            Send(s, MessageProtocol.Tcp);
        }

        public void Send(String s, MessageProtocol reli)
        {
            byte[] b = gmc.Encode(s);
            byte[] c = new byte[b.Length + 1];
            Array.Copy(b, 0, c, 1, b.Length);
            c[0] = (byte)GMCMessageType.String;
            stream.Send(c, reli);
        }

        void gmc_SendTemplateEvent(byte[] data)
        {
            byte[] b = new byte[data.Length + 1];
            data.CopyTo(b,1);
            b[0] = (byte)GMCMessageType.Templates;
            stream.Send(b);
        }

        void gmc_SendFrequenciesEvent(byte[] data)
        {
            byte[] b = new byte[data.Length + 1];
            data.CopyTo(b, 1);
            b[0] = (byte)GMCMessageType.Frequencies;
            stream.Send(b);
        }

        void gmc_SendAnnouncementEvent(byte[] data)
        {
            byte[] b = new byte[data.Length + 1];
            data.CopyTo(b, 1);
            b[0] = (byte)GMCMessageType.Announcements;
            stream.Send(b);
        }
        
    }
}
