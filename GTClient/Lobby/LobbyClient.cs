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
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using GT.Net;

namespace Lobby
{
    [Serializable]
    public class ServerInfo : IEquatable<ServerInfo>
    {
        public string Name;
        public string IPAddress;
        public string Port;
        public List<string> Participants;
        public bool InProgress;

        public bool Equals(ServerInfo si)
        {
            if (si == null)
                return false;
            if (si.Name == Name && si.Port == Port && si.IPAddress == IPAddress)
                return true;
            return false;
        }
    }

    [Serializable]
    internal class ServerPersonTuple
    {
        public string Server;
        public string Person;

        public ServerPersonTuple(string server, string person)
        {
            this.Server = server;
            this.Person = person;
        }
    }

    public class LobbyClient
    {
        private SimpleSharedDictionary sharedDictionary;
        private Form form;
        private ListView serverView;
        private string myName;

        public ServerInfo JoinedServer;

        public LobbyClient(string myName, SimpleSharedDictionary sharedDictionary)
        {
            lock (this)
            {
                //grr
                Form.CheckForIllegalCrossThreadCalls = false;
                this.myName = myName;

                this.sharedDictionary = sharedDictionary;

                form = new Form();
                form.TopMost = true;
                serverView = new ListView();
                serverView.MultiSelect = false;
                serverView.ShowItemToolTips = true;
                serverView.Sorting = SortOrder.Ascending;
                serverView.GridLines = true;
                serverView.FullRowSelect = true;
                serverView.Activation = ItemActivation.Standard;
                serverView.Name = "lvServerView";
                serverView.Columns.Add("Name", 100, HorizontalAlignment.Left);
                serverView.Columns.Add("Players", 200, HorizontalAlignment.Left);
                serverView.Dock = DockStyle.Fill;
                serverView.View = View.Details;
                serverView.SmallImageList = new ImageList();
                form.Controls.Add(serverView);
            }

            serverView.MouseDoubleClick += new MouseEventHandler(MouseDoubleClick);
            sharedDictionary.ChangeEvent += new SimpleSharedDictionary.Change(ChangeEvent);
            sharedDictionary["EveryoneIsInformed"] = false;
        }

        void MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (sender.GetType() == typeof(ListView))
            {
                ListView listView = (ListView)sender;
                ListViewItem lvi = listView.GetItemAt(e.X, e.Y);
                if(JoinedServer == null)
                    sharedDictionary["Join"] = new ServerPersonTuple(lvi.Name, this.myName);
                else if (JoinedServer != null)
                    sharedDictionary["Leave"] = new ServerPersonTuple(lvi.Name, this.myName);
            }
        }

        public void Show()
        {
            form.Show();
        }

        public DialogResult ShowDialog()
        {
            form.ShowDialog();
            return form.DialogResult;
        }

        private void UpdateEntry(string key, ServerInfo server)
        {
            string playerList = "";

            //if we've been booted, leave
            if (server.Equals(JoinedServer) && !server.Participants.Contains(myName))
            {
                JoinedServer = null;
                serverView.Items[key].ForeColor = Color.Black;
            }

            //if we've been invited, join
            if (null == JoinedServer && server.Participants.Contains(myName))
            {
                JoinedServer = server;
                serverView.Items[key].ForeColor = Color.Green;
            }

            if (server.Equals(JoinedServer) && server.Participants.Contains(myName) && server.InProgress)
            {
                Console.WriteLine("Joining game in-progress!");
                form.DialogResult = DialogResult.OK;
            }

            //display!
            foreach (string s in server.Participants)
                playerList += s + " ";
            serverView.Items[key].SubItems[1].Text = playerList;
        }

        private void ChangeEvent(string key)
        {
            switch (key)
            {
                //ignore these cases
                case "Leave":
                case "EveryoneIsInformed":
                case "Join": 
                    break;
                //but we're interested in this one
                default:
                    if (sharedDictionary[key].GetType() != typeof(ServerInfo)) 
                        break; 
                    ServerInfo server = (ServerInfo)sharedDictionary[key];
                    if (!serverView.Items.ContainsKey(key))
                    {
                        ListViewItem lvi = new ListViewItem();
                        lvi.Name = key;
                        lvi.Text = server.Name;
                        lvi.ToolTipText = "Double-click to join server.";
                        lvi.ForeColor = Color.CadetBlue;
                        lvi.SubItems.Insert(1, new ListViewItem.ListViewSubItem(lvi, "None"));
                        serverView.Items.Add(lvi);
                    }
                    UpdateEntry(key, server);
                    break;
            }
        }

        ~LobbyClient()
        {
            //the dictionary may still be in use.  just remove us.
            sharedDictionary.ChangeEvent -= new SimpleSharedDictionary.Change(ChangeEvent);

            try
            {
                //unload form
                form.Dispose();
            }
            catch (NullReferenceException) { }
        }

    }
}
