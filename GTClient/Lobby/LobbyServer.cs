using System;
using System.Collections.Generic;
using GT.Net;

namespace Lobby
{
    public class LobbyServer
    {
        private ServerInfo server;
        private SimpleSharedDictionary sharedDictionary;
        private string myKey;

        public LobbyServer(string name, string address, string port, SimpleSharedDictionary sharedDictionary)
        {
            myKey = address + ":" + port + ":" + name;
            this.sharedDictionary = sharedDictionary;
            this.server = new ServerInfo();
            this.server.Name = name;
            this.server.IPAddress = address;
            this.server.Port = port;
            this.server.Participants = new List<string>();
            this.server.InProgress = false;

            sharedDictionary.Master.Add(myKey);
            sharedDictionary[myKey] = server;

            sharedDictionary.Changed += new SimpleSharedDictionary.Change(ChangeEvent);
        }

        void ChangeEvent(string key)
        {
            switch (key)
            {
                case "Leave":
                    if (sharedDictionary[key].GetType() == typeof(ServerPersonTuple))
                        if (((ServerPersonTuple)sharedDictionary[key]).Server == myKey)
                        {
                            ServerPersonTuple spt = (ServerPersonTuple)sharedDictionary[key];
                            ServerInfo si = (ServerInfo)sharedDictionary[myKey];
                            if(si.Participants.Contains(spt.Person))
                                si.Participants.Remove(spt.Person);
                            sharedDictionary[myKey] = si;
                        }
                    break;
                case "Join":
                    if (sharedDictionary[key].GetType() == typeof(ServerPersonTuple))
                        if (((ServerPersonTuple)sharedDictionary[key]).Server == myKey)
                        {
                            ServerPersonTuple spt = (ServerPersonTuple)sharedDictionary[key];
                            ServerInfo si = (ServerInfo)sharedDictionary[myKey];
                            si.Participants.Add(spt.Person);
                            sharedDictionary[myKey] = si;
                        }
                    break;
                case "EveryoneIsInformed":
                    if (sharedDictionary[key].GetType() == typeof(bool))
                        if (!(bool)sharedDictionary[key])
                            sharedDictionary[server.IPAddress + ":" + server.Port + ":" + server.Name] = server;
                    break;
            }
        }

        ~LobbyServer()
        {
            sharedDictionary.Master.Remove(myKey);
            sharedDictionary.Changed -= new SimpleSharedDictionary.Change(ChangeEvent);
        }

        public string[] Participants
        {
            get
            {
                return server.Participants.ToArray();
            }
        }

        public bool InProgress
        {
            get
            {
                return server.InProgress;
            }

            set
            {
                server.InProgress = value;
                sharedDictionary[myKey] = server;
            }
        }

        public static string GetMyIPAddress()
        {
            string[] s;
            char[] delimDot = {'.'};
            System.Net.IPHostEntry he = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (System.Net.IPAddress e in he.AddressList)
            {
                try 
                {
                    s = e.ToString().Split(delimDot);
                    if (s.Length < 4)
                        continue;
                    s[0] = Byte.Parse(s[0]).ToString();
                    s[1] = Byte.Parse(s[1]).ToString();
                    s[2] = Byte.Parse(s[2]).ToString();
                    s[3] = Byte.Parse(s[3]).ToString();
                    return String.Join(".", s); ; 
                }
                catch { }
            }
            return null;
        }

    }
}
