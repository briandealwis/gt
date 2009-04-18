using System;
using System.Collections.Generic;
using System.Text;
using GT.Net;
using GMCException = GT.GMC.MissingInformationException;

namespace GT.GMC
{
    //public class CompressedStringChannel : IStringChannel
    //{
    //    private struct SavedMessage
    //    {
    //        public int UserID;
    //        public byte[] Payload;
    //        public GMCOrdering Ordering;
    //        public GMCReliability Reli;
    //        public int ArrivalTime;

    //        public SavedMessage(int currentTime, int userID, GMCOrdering ordering, GMCReliability reli, byte[] payload)
    //        {
    //            this.UserID = userID;
    //            this.Payload = payload;
    //            this.Ordering = ordering;
    //            this.Reli = reli;
    //            this.ArrivalTime = currentTime;
    //        }

    //        public static SavedMessage Blank = new SavedMessage();
    //    }



    //    private enum GMCMessageType
    //    {
    //        Announcements = 0,
    //        FrequencyTable = 1,
    //        Template = 2,
    //        String = 3,
    //        MissingAnnouncements = 4,
    //        MissingFrequencies = 5,
    //        MissingTemplates = 6
    //    }

    //    private enum GMCOrdering
    //    {
    //        InOrder = 0,
    //        OutOfOrder = 8
    //    }

    //    private enum GMCReliability
    //    {
    //        Reliable = 0,
    //        Unreliable = 16
    //    }

    //    private enum GMCDestinationType
    //    {
    //        Directed = 0,
    //        Broadcast = 32
    //    }

    //    public enum GMCTimeliness
    //    {
    //        NonRealTime = 0,
    //        RealTime = 64
    //    }

    //    protected GMCMarshaller gmc;
    //    protected IBinaryChannel channel;
    //    private Dictionary<byte[], int> failedIncomingMessages;
    //    private List<SavedMessage> waitingOutOfOrderMessages;
    //    private List<SavedMessage> waitingInOrderMessages;
    //    private Dictionary<int, Dictionary<int, Dictionary<byte, int>>> ourAnnouncementRequestingTimes;
    //    private Dictionary<int, Dictionary<int, int>> ourTemplateRequestingTimes;
    //    private Dictionary<int, Dictionary<int, Dictionary<byte, int>>> ourAnnouncementSendingTimes;
    //    private Dictionary<int, Dictionary<int, int>> ourTemplateSendingTimes;
    //    private static byte maskGMCMessageType = 7;
    //    private static byte maskGMCOrdering = 8;
    //    private static byte maskGMCReliability = 16;
    //    private static byte maskGMCDestinationType = 32;
    //    private static byte maskGMCTimeliness = 64;
    //    private static int userTimeout = 5000;
    //    private List<string> messages;

    //    public CompressedStringChannel(IBinaryChannel channel)
    //    {
    //        messages = new List<string>();
    //        failedIncomingMessages = new Dictionary<byte[], int>();
    //        waitingInOrderMessages = new List<SavedMessage>();
    //        waitingOutOfOrderMessages = new List<SavedMessage>();
    //        ourAnnouncementRequestingTimes = new Dictionary<int, Dictionary<int, Dictionary<byte, int>>>();
    //        ourTemplateRequestingTimes = new Dictionary<int, Dictionary<int, int>>();
    //        ourAnnouncementSendingTimes = new Dictionary<int, Dictionary<int, Dictionary<byte, int>>>();
    //        ourTemplateSendingTimes = new Dictionary<int, Dictionary<int, int>>();
    //        gmc = new GMCMarshaller();
    //        this.channel = channel;
    //        channel.NewMessageEvent += channel_BinaryNewMessageEvent;
    //    }

    //    public float Delay { get { return channel.Delay; } }

    //    public List<string> Messages { get { return messages; } }

    //    public int Identity { get { return channel.Identity; } }

    //    public event StringNewMessage StringNewMessageEvent;

    //    public String DequeueMessage(int index)
    //    {
    //        if (messages.Count <= 0)
    //            return null;
    //        string s = messages[index];
    //        messages.RemoveAt(index);
    //        return s;
    //    }

    //    public void Send(String s)
    //    {
    //        SendMessage(s, MessageProtocol.Tcp, MessageAggregation.No, MessageOrder.AllChannel, GMCTimeliness.NonRealTime);
    //    }

    //    public void Send(String s, MessageProtocol reli)
    //    {
    //        SendMessage(s, reli, MessageAggregation.No, MessageOrder.AllChannel, GMCTimeliness.NonRealTime);
    //    }

    //    public void Send(String s, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering)
    //    {
    //        SendMessage(s, reli, aggr, ordering, GMCTimeliness.NonRealTime);
    //    }

    //    public void Send(String s, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering, GMCTimeliness timeliness)
    //    {
    //        SendMessage(s, reli, aggr, ordering, timeliness);
    //    }

    //    public void FlushAllOutgoingMessagesOnChannel(MessageProtocol protocol)
    //    {
    //        channel.FlushAllOutgoingMessagesOnChannel(protocol);
    //    }

    //    public bool Dead
    //    {
    //        get { return !channel.Active; }
    //    }

    //    /// <summary>Checks to see if we've requested these announcements recently.  
    //    /// Returns true if we should send a request for any of these announcements, and false if all of them 
    //    /// have been recently requested</summary>
    //    /// <param name="currentTime">The current time in milliseconds</param>
    //    /// <param name="e">The exception where the missing announcements are stored</param>
    //    /// <returns>True if we should send a request for any of these announcements, and false if all of them 
    //    /// have been recently requested</returns>
    //    private bool IsOurAnnouncementRequestNew(int currentTime, GMCException e)
    //    {
    //        Dictionary<int, Dictionary<byte, int>> templates;
    //        Dictionary<byte, int> shortcuts;
    //        int time;
    //        bool isNew;

    //        //if we know this user already
    //        if (ourAnnouncementRequestingTimes.TryGetValue(e.UserID, out templates))
    //        {
    //            //if we know this template already
    //            if (templates.TryGetValue(e.Template, out shortcuts))
    //            {
    //                isNew = false;
    //                for (int i = 0; i < e.IDs.Count; i++)
    //                {
    //                    //and we know this shortcut
    //                    if (shortcuts.TryGetValue(e.IDs[i], out time))
    //                    {
    //                        //and if we haven't recently requested it
    //                        if (time + 1000 < currentTime)
    //                        {
    //                            isNew = true;
    //                        }
    //                    }
    //                    else
    //                    {
    //                        //else if we don't know this shortcut, request it and remember that we did so
    //                        isNew = true;
    //                        shortcuts.Add(e.IDs[i], currentTime);
    //                    }
    //                }

    //                //false, these announcements are not new
    //                if (!isNew)
    //                    return false;

    //                //update the times
    //                for (int i = 0; i < e.IDs.Count; i++)
    //                {
    //                    shortcuts[e.IDs[i]] = currentTime;
    //                }
    //            }
    //            else
    //            {
    //                //else if we don't know this template, request it and remember that we did so
    //                templates.Add(e.Template, new Dictionary<byte, int>());
    //                for (int i = 0; i < e.IDs.Count; i++)
    //                    if (!templates[e.Template].ContainsKey(e.IDs[i]))
    //                        templates[e.Template].Add(e.IDs[i], currentTime);
    //            }
    //        }
    //        else
    //        {
    //            //else if we don't know this user already, then remember the user, the template, and these IDs
    //            ourAnnouncementRequestingTimes.Add(e.UserID, new Dictionary<int, Dictionary<byte,int>>());
    //            ourAnnouncementRequestingTimes[e.UserID].Add(e.Template, new Dictionary<byte, int>());
    //            for (int i = 0; i < e.IDs.Count; i++)
    //                if (!ourAnnouncementRequestingTimes[e.UserID][e.Template].ContainsKey(e.IDs[i]))
    //                    ourAnnouncementRequestingTimes[e.UserID][e.Template].Add(e.IDs[i], currentTime);
    //        }

    //        //true, some or all of these announcements are new
    //        return true;
    //    }

    //    /// <summary>Checks to see if we've requested this template recently.  
    //    /// Returns false if we've recently sent a request for this template, and true if 
    //    /// this template request hasn't been sent in a while.</summary>
    //    /// <param name="currentTime">The current time in milliseconds</param>
    //    /// <param name="e">The exception where the missing template is stored</param>
    //    /// <returns>False if we've recently sent a request for this template, true if this template request hasn't been sent in a while.</returns>
    //    private bool IsOurTemplateRequestNew(int currentTime, GMCException e)
    //    {
    //        int time;

    //        Dictionary<int, int> templates;
    //        //if we know about this user, grab the template
    //        if (ourTemplateRequestingTimes.TryGetValue(e.UserID, out templates))
    //        {
    //            if (templates.TryGetValue(e.Template, out time))
    //            {
    //                //if we've recently sent a request, don't do it again until some time has passed
    //                if (time + 1000 > currentTime)
    //                {
    //                    //false, this template request is not new because we've recently seen it before
    //                    return false;
    //                }
    //                //else, update the time
    //                templates[e.Template] = currentTime;
    //            }
    //            else
    //            {
    //                templates.Add(e.Template, time);
    //            }
    //        }
    //        else
    //        {
    //            //else if we don't, create one
    //            ourTemplateRequestingTimes.Add(e.UserID, new Dictionary<int, int>());
    //            ourTemplateRequestingTimes[e.UserID].Add(e.Template, currentTime);
    //        }
    //        //true, we haven't seen this template request in a while
    //        return true;
    //    }

    //    /// <summary>Checks to see if we've sent these announcements recently.  
    //    /// Returns true if we should send any of these announcements, and false if all of them 
    //    /// have been recently sent already</summary>
    //    /// <param name="currentTime">The current time in milliseconds</param>
    //    /// <param name="userID">The user we're sending to</param>
    //    /// <returns>True if we should send these announcements, and false if all of them 
    //    /// have been recently sent already</returns>
    //    private bool IsOurAnnouncementSendingNew(int currentTime, int userID, int templateID, byte[] announcementShortcuts)
    //    {
    //        Dictionary<int, Dictionary<byte, int>> templates;
    //        Dictionary<byte, int> shortcuts;
    //        int time;
    //        bool isNew;

    //        //if we know this user already
    //        if (ourAnnouncementSendingTimes.TryGetValue(userID, out templates))
    //        {
    //            //if we know this template already
    //            if (templates.TryGetValue(templateID, out shortcuts))
    //            {
    //                isNew = false;
    //                for (int i = 0; i < announcementShortcuts.Length; i++)
    //                {
    //                    //and we know this shortcut
    //                    if (shortcuts.TryGetValue(announcementShortcuts[i], out time))
    //                    {
    //                        //and if we haven't recently requested it
    //                        if (time + 1000 < currentTime)
    //                        {
    //                            isNew = true;
    //                        }
    //                    }
    //                    else
    //                    {
    //                        //else if we don't know this shortcut, request it and remember that we did so
    //                        isNew = true;
    //                        shortcuts.Add(announcementShortcuts[i], currentTime);
    //                    }
    //                }

    //                //false, these announcements are not new
    //                if (!isNew)
    //                    return false;

    //                //update the times
    //                for (int i = 0; i < announcementShortcuts.Length; i++)
    //                {
    //                    shortcuts[announcementShortcuts[i]] = currentTime;
    //                }
    //            }
    //            else
    //            {
    //                //else if we don't know this template, request it and remember that we did so
    //                templates.Add(templateID, new Dictionary<byte, int>());
    //                for (int i = 0; i < announcementShortcuts.Length; i++)
    //                    if (!templates[templateID].ContainsKey(announcementShortcuts[i]))
    //                        templates[templateID].Add(announcementShortcuts[i], currentTime);
    //            }
    //        }
    //        else
    //        {
    //            //else if we don't know this user already, then remember the user, the template, and these IDs
    //            ourAnnouncementSendingTimes.Add(userID, new Dictionary<int, Dictionary<byte, int>>());
    //            ourAnnouncementSendingTimes[userID].Add(templateID, new Dictionary<byte, int>());
    //            for (int i = 0; i < announcementShortcuts.Length; i++)
    //                if (!ourAnnouncementSendingTimes[userID][templateID].ContainsKey(announcementShortcuts[i]))
    //                    ourAnnouncementSendingTimes[userID][templateID].Add(announcementShortcuts[i], currentTime);
    //        }

    //        //true, some or all of these announcements are new
    //        return true;
    //    }

    //    private bool IsOurTemplateSendingNew(int currentTime, int userID, int templateID)
    //    {
    //        int time;

    //        Dictionary<int, int> templates;
    //        if (ourTemplateSendingTimes.TryGetValue(userID, out templates))
    //        {
    //            if (templates.TryGetValue(templateID, out time))
    //            {
    //                //if we've recently sent a request, don't do it again until some time has passed
    //                if (time + 1000 > currentTime)
    //                {
    //                    //false, this template request is not new because we've recently seen it before
    //                    return false;
    //                }
    //                templates[templateID] = currentTime;
    //            }
    //            else
    //            {
    //                templates.Add(templateID, time);
    //            }
    //        }
    //        else
    //        {
    //            ourTemplateSendingTimes.Add(userID, new Dictionary<int, int>());
    //            ourTemplateSendingTimes[userID].Add(templateID, currentTime);
    //        }
    //        //true, we haven't seen this template request in a while
    //        return true;
    //    }

    //    private void HandleCompressorException(int currentTime, GMCException e)
    //    {
    //        byte[] b = new byte[13 + e.IDs.Count];
    //        switch (e.ExceptionType)
    //        {
    //            case EnumExceptionType.MissingAnnouncement:
    //                //If these announcements have already been requested, don't re-request them.
    //                //Don't want to flood the network!
    //                if (!IsOurAnnouncementRequestNew(currentTime, e))
    //                    return;
    //                Console.WriteLine(System.Environment.TickCount + " Requesting announcements from user " + e.UserID);
    //                b[4] |= (int)GMCMessageType.MissingAnnouncements;
    //                break;
    //            case EnumExceptionType.MissingFrequencyTable:
    //                b[4] |= (int)GMCMessageType.MissingFrequencies; 
    //                break;
    //            case EnumExceptionType.MissingTemplate:
    //                //If this template has already been requested, don't re-request them.
    //                //Don't want to flood the network!
    //                if (!IsOurTemplateRequestNew(currentTime, e))
    //                    return;
    //                Console.WriteLine(System.Environment.TickCount + " Requesting template " + e.Template + " from user " + e.UserID);
    //                b[4] |= (int)GMCMessageType.MissingTemplates; 
    //                break;
    //            default:
    //                //no weirdness is allowed here
    //                Console.WriteLine("This should never happen.");
    //                return;
    //        }

    //        DataConverter.Converter.GetBytes(e.UserID).CopyTo(b, 0);
    //        DataConverter.Converter.GetBytes(e.Template).CopyTo(b, 5);
    //        DataConverter.Converter.GetBytes(channel.Identity).CopyTo(b, 9);

    //        int count = e.IDs.Count;
    //        for (int i = 0; i < count; i++)
    //            b[13 + i] = e.IDs[i];

    //        channel.Send(b);
    //    }

    //    private void HandleNewCompressedMessage(GMCTimeliness timeliness, SavedMessage msg)
    //    {
    //        if (msg.Ordering == GMCOrdering.InOrder && waitingInOrderMessages.Count > 0)
    //        {
    //            waitingInOrderMessages.Add(msg);
    //            return;
    //        }

    //        try
    //        { //try to decompress the payload
    //            messages.Add(gmc.DecodeString(msg.UserID, msg.Payload));
    //        }
    //        catch (GMCException e)
    //        {
    //            if(timeliness == GMCTimeliness.NonRealTime)
    //            {
    //                if (msg.Ordering == GMCOrdering.InOrder)
    //                    waitingInOrderMessages.Add(msg);
    //                else
    //                    waitingOutOfOrderMessages.Add(msg);
    //            }


    //            HandleCompressorException(msg.ArrivalTime, e);
    //        }
    //    }

    //    /// <summary>Checks the first message in the list and tries to decompress it.
    //    /// If it succeeds, it checks the next in the list, 
    //    /// </summary>
    //    private bool HandleOldCompressedInOrderMessages(int currentTime)
    //    {
    //        bool newMessages = false;
    //        SavedMessage msg = SavedMessage.Blank;
    //        while(waitingInOrderMessages.Count > 0)
    //        {
    //            try
    //            {
    //                msg = waitingInOrderMessages[0];
    //                string s = gmc.DecodeString(msg.UserID, msg.Payload);
    //                messages.Add(s);
    //                waitingInOrderMessages.RemoveAt(0);
    //                newMessages = true;
    //            }
    //            catch (GMCException e)
    //            {
    //                //if (msg.ArrivalTime + userTimeout < currentTime)
    //                    //HandleUserLeft(msg.UserID);
    //                //else
    //                    HandleCompressorException(currentTime, e);

    //                break;
    //            }
    //        }
    //        return newMessages;
    //    }

    //    /// <summary>Checks each message in the list and tries to decompress it.</summary>
    //    private bool HandleOldCompressedOutOfOrderMessages(int currentTime)
    //    {
    //        bool newMessages = false;
    //        SavedMessage msg = SavedMessage.Blank;
    //        for (int i = 0; i < waitingOutOfOrderMessages.Count; i++)
    //        {
    //            string s;
    //            msg = waitingOutOfOrderMessages[i];
    //            try
    //            {
    //                s = gmc.DecodeString(msg.UserID, msg.Payload);
    //                messages.Add(s);
    //                waitingOutOfOrderMessages.RemoveAt(i);
    //                i--;
    //                newMessages = true;
    //            }
    //            catch (GMCException e)
    //            {
    //                //if (msg.ArrivalTime + userTimeout < currentTime)
    //                    //HandleUserLeft(msg.UserID);
    //                //else
    //                    HandleCompressorException(currentTime, e);
    //            }
    //        }
    //        return newMessages;
    //    }

    //    /// <summary>
    //    /// When a user leaves, we shouldn't ask after them anymore.  Doing so locks stupid clients.  Instead, 
    //    /// detect a timeout, then erase them from our memory.
    //    /// </summary>
    //    /// <param name="userID"></param>
    //    private void HandleUserLeft(int userID)
    //    {
    //        //remove all messages waiting from this user
    //        for (int i = 0; i < waitingInOrderMessages.Count; i++)
    //            if (waitingInOrderMessages[i].UserID == userID)
    //            {
    //                waitingInOrderMessages.RemoveAt(i);
    //                i--;
    //            }
    //        for (int i = 0; i < waitingOutOfOrderMessages.Count; i++)
    //            if (waitingOutOfOrderMessages[i].UserID == userID)
    //            {
    //                waitingOutOfOrderMessages.RemoveAt(i);
    //                i--;
    //            }

    //        //remove the user from gmc
    //        gmc.RemoveUserID(userID);
    //        if(ourAnnouncementRequestingTimes.ContainsKey(userID))
    //            ourAnnouncementRequestingTimes.Remove(userID);
    //        if (ourTemplateRequestingTimes.ContainsKey(userID))
    //            ourTemplateRequestingTimes.Remove(userID);
    //        if (ourAnnouncementSendingTimes.ContainsKey(userID))
    //            ourAnnouncementSendingTimes.Remove(userID);
    //        if (ourTemplateSendingTimes.ContainsKey(userID))
    //            ourTemplateSendingTimes.Remove(userID);
    //    }

    //    private bool ReceiveUpdate(int currentTime, GMCMessageType messageType, int userID, byte[] messagePayload)
    //    {
    //        try
    //        {
    //            switch (messageType)
    //            {
    //                case GMCMessageType.Announcements:
    //                    List<byte[]> list = new List<byte[]>();
    //                    int count = messagePayload.Length / 7;
    //                    byte[] entry;
    //                    for (int i = 0; i < count; i++)
    //                    {
    //                        entry = new byte[7];
    //                        Array.Copy(messagePayload, i * 7, entry, 0, 7);
    //                        list.Add(entry);
    //                        Console.WriteLine(currentTime + " Receiving announcement: name:" +
    //                              DataConverter.Converter.ToInt16(entry, 0) +
    //                              " longForm: " +
    //                              DataConverter.Converter.ToInt32(entry, 2) + " shortForm: " + entry[6]);
    //                    }
    //                    gmc.ReceiveAnnouncements(list, userID);
    //                    break;
    //                case GMCMessageType.FrequencyTable:
    //                    gmc.ReceiveFrequencyTable(messagePayload, userID);
    //                    break;
    //                case GMCMessageType.Template:
    //                    Console.WriteLine(currentTime + " Receiving template: " + UTF8Encoding.UTF8.GetString(messagePayload));
    //                    gmc.ReceiveTemplate(UTF8Encoding.UTF8.GetString(messagePayload), userID);
    //                    break;
    //            }
    //        }
    //        catch (GMCException e)
    //        {
    //            HandleCompressorException(currentTime, e);
    //            return false;
    //        }

    //        return true;
    //    }

    //    private void ReceiveRequest(int currentTime, GMCMessageType messageType, int userID, int templateID, int requesteeID, byte[] messagePayload)
    //    {
    //        if (userID != channel.Identity)
    //            return;

    //        int count;
    //        byte[] b;
    //        byte[] reply;
    //        switch (messageType)
    //        {
    //            case GMCMessageType.MissingAnnouncements:
    //                if(!IsOurAnnouncementSendingNew(currentTime, requesteeID, templateID, messagePayload))
    //                    return;
    //                List<byte[]> announcements = gmc.GetAnnouncements(templateID, messagePayload);
    //                count = announcements.Count;
    //                b = new byte[count * 7]; //number of announcements times the size of an announcement
    //                for (int i = 0; i < count; i++)
    //                {
    //                    announcements[i].CopyTo(b, i * 7);
    //                    Console.WriteLine(currentTime + " Sending announcement after request: name:" + DataConverter.Converter.ToInt16(announcements[i], 0) + " longForm: " + DataConverter.Converter.ToInt32(announcements[i], 2) + " shortForm: " + announcements[i][6]);
    //                }
    //                reply = new byte[13 + b.Length];
    //                reply[4] |= (byte)GMCMessageType.Announcements;
    //                b.CopyTo(reply, 13);
    //                break;
    //            case GMCMessageType.MissingTemplates:
    //                Console.WriteLine("MissingTemplate");
    //                if(!IsOurTemplateSendingNew(currentTime, requesteeID, templateID))
    //                    return;
    //                b = UTF8Encoding.UTF8.GetBytes(gmc.GetTemplate(templateID));
    //                reply = new byte[13 + b.Length];
    //                reply[4] |= (byte)GMCMessageType.Template;
    //                b.CopyTo(reply, 13);
    //                Console.WriteLine(currentTime + " Sending template after request: " + UTF8Encoding.UTF8.GetString(b));
    //                break;
    //            default:
    //                //do nothing, because weirdness is not accepted here!
    //                return;
    //        }

    //        DataConverter.Converter.GetBytes(userID).CopyTo(reply, 0);
    //        reply[4] |= (byte)GMCDestinationType.Directed;
    //        DataConverter.Converter.GetBytes(templateID).CopyTo(reply, 5);
    //        DataConverter.Converter.GetBytes(requesteeID).CopyTo(reply, 8);

    //        channel.Send(reply);
    //    }

    //    void channel_BinaryNewMessageEvent(IBinaryChannel channel)
    //    {
    //        lock (this)
    //        {
    //            byte[] b;
    //            int currentTime = System.Environment.TickCount;
    //            bool gmcUpdated = false, newMessages = false;
    //            while ((b = channel.DequeueMessage(0)) != null)
    //            {
    //                try
    //                {
    //                    GMCMessageType messageType = (GMCMessageType)(b[4] & maskGMCMessageType);
    //                    int userID = DataConverter.Converter.ToInt32(b, 0);
    //                    int templateID, requesteeID;
    //                    byte[] messagePayload;

    //                    switch (messageType)
    //                    {
    //                        case GMCMessageType.Announcements:
    //                            messagePayload = new byte[b.Length - 13];
    //                            Array.Copy(b, 13, messagePayload, 0, messagePayload.Length);
    //                            gmcUpdated |= ReceiveUpdate(currentTime, messageType, userID, messagePayload);
    //                            //if we get a new announcement, then maybe we can decode some old messages!
    //                            break;

    //                        case GMCMessageType.FrequencyTable:
    //                        case GMCMessageType.Template:
    //                            messagePayload = new byte[b.Length - 13];
    //                            Array.Copy(b, 13, messagePayload, 0, messagePayload.Length);
    //                            ReceiveUpdate(currentTime, messageType, userID, messagePayload);
    //                            break;

    //                        case GMCMessageType.MissingAnnouncements:
    //                        case GMCMessageType.MissingFrequencies:
    //                        case GMCMessageType.MissingTemplates:
    //                            templateID = DataConverter.Converter.ToInt32(b, 5);
    //                            requesteeID = DataConverter.Converter.ToInt32(b, 9);
    //                            messagePayload = new byte[b.Length - 13];
    //                            Array.Copy(b, 13, messagePayload, 0, messagePayload.Length);
    //                            ReceiveRequest(currentTime, messageType, userID, templateID, requesteeID, messagePayload);
    //                            break;

    //                        default:
    //                            messagePayload = new byte[b.Length - 5];
    //                            Array.Copy(b, 5, messagePayload, 0, messagePayload.Length);
    //                            GMCOrdering messageOrdering = (GMCOrdering)(b[4] & maskGMCOrdering);
    //                            GMCReliability messageReliability = (GMCReliability)(b[4] & maskGMCReliability);
    //                            GMCTimeliness messageTimeliness = (GMCTimeliness)(b[4] & maskGMCTimeliness);
    //                            HandleNewCompressedMessage(messageTimeliness,
    //                                new SavedMessage(currentTime, userID, messageOrdering, messageReliability, messagePayload));
    //                            newMessages = true;
    //                            break;
    //                    }
    //                }
    //                catch (Exception)
    //                {
    //                    Console.WriteLine("We must have received bad data?");
    //                }
    //            }

    //            if (gmcUpdated)
    //            {
    //                newMessages = HandleOldCompressedInOrderMessages(currentTime) || HandleOldCompressedOutOfOrderMessages(currentTime);
    //            }

    //            if (StringNewMessageEvent != null && newMessages)
    //                StringNewMessageEvent(this);

    //        }
    //    }

    //    protected void SendMessage(String s, MessageProtocol reli, MessageAggregation aggr, MessageOrder ordering, GMCTimeliness timeliness)
    //    {
    //        //don't do a damn thing until we're ready
    //        if (channel.Identity == 0)
    //            throw new Exception("Our Identity for this server equals zero, therefore we have not received our identity from the server yet."+
    //                "  Please do not compress anything until we know our unique identity, so that others know who we are.");

    //        lock (this)
    //        {
    //            CompressedMessagePackage co = gmc.Encode(s);
    //            byte[] data;

    //            //co = new CompressedMessagePackage();
    //            //co.Message = UTF8Encoding.UTF8.GetBytes("Hi!");

    //            if (co.Template != null)
    //            {
    //                data = new byte[co.Template.Length + 13];
    //                DataConverter.Converter.GetBytes(Identity).CopyTo(data, 0);
    //                data[4] |= (byte)GMCMessageType.Template;
    //                data[4] |= (byte)GMCDestinationType.Broadcast;
    //                co.Template.CopyTo(data, 13);
    //                channel.Send(data, MessageProtocol.Tcp, MessageAggregation.Yes, MessageOrder.AllChannel);
    //                Console.WriteLine(System.Environment.TickCount + " Sending template: " + UTF8Encoding.UTF8.GetString(co.Template));
    //            }

    //            //I have plans for this foreach.  They involve treating Broadcast announcements differently from Direct announcements,
    //            //therefore eliminating the blank space in data[5-12].
    //            if (co.Announcements.Count > 0)
    //            {
    //                data = new byte[co.Announcements.Count * 7 + 13];
    //                DataConverter.Converter.GetBytes(Identity).CopyTo(data, 0);
    //                data[4] |= (byte)GMCMessageType.Announcements;
    //                data[4] |= (byte)GMCDestinationType.Broadcast;
    //                for (int i = 0; i < co.Announcements.Count; i++)
    //                {
    //                    co.Announcements[i].CopyTo(data, 13 + i * 7);
    //                    Console.WriteLine(System.Environment.TickCount + " Sending announcement: name:" + DataConverter.Converter.ToInt16(co.Announcements[i], 0) + " longForm: " + DataConverter.Converter.ToInt32(co.Announcements[i], 2) + " shortForm: " + co.Announcements[i][6]);
    //                }
    //                channel.Send(data, MessageProtocol.Tcp, MessageAggregation.Yes, MessageOrder.AllChannel);
    //            }

    //            if (co.FrequencyTable != null)
    //            {
    //                data = new byte[co.FrequencyTable.Length + 13];
    //                DataConverter.Converter.GetBytes(Identity).CopyTo(data, 0);
    //                data[4] |= (byte)GMCMessageType.FrequencyTable;
    //                data[4] |= (byte)GMCDestinationType.Broadcast;
    //                co.FrequencyTable.CopyTo(data, 13);
    //                channel.Send(data, MessageProtocol.Tcp, MessageAggregation.Yes, MessageOrder.AllChannel);
    //            }

    //            data = new byte[co.Message.Length + 5];
    //            data[4] |= (byte)GMCMessageType.String;
    //            data[4] |= (byte)GMCDestinationType.Broadcast;
    //            data[4] |= (byte)timeliness;
    //            if (ordering == MessageOrder.None)
    //                data[4] |= (byte)GMCOrdering.OutOfOrder;
    //            else
    //                data[4] |= (byte)GMCOrdering.InOrder;
    //            DataConverter.Converter.GetBytes(channel.Identity).CopyTo(data, 0);
    //            co.Message.CopyTo(data, 5);

    //            //make sure the announcements and such get sent first
    //            if (reli != MessageProtocol.Tcp || ordering == MessageOrder.None)
    //                channel.FlushAllOutgoingMessagesOnChannel(MessageProtocol.Tcp);

    //            channel.Send(data, reli, aggr, ordering);
    //        }
    //    }
    //}
}
