using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using GTClient;

namespace SharedDictionaryTest
{
    public partial class frmSharedDictionaryTestForm : Form
    {
        Client c;
        SimpleSharedDictionary sd;

        public frmSharedDictionaryTestForm()
        {
            InitializeComponent();

            c = new Client();
            sd = new SimpleSharedDictionary(c.GetBinaryStream("127.0.0.1", "9999", 0));
            sd.ChangeEvent += new SimpleSharedDictionary.Change(sd_ChangeEvent);
        }

        void sd_ChangeEvent(string key)
        {
            lock ("weee!")
            {
                txtInput.Text = (string)sd["text"];
            }
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            lock ("weee!")
            {
                c.Update();
            }
        }

        private void txtInput_KeyUp(object sender, KeyEventArgs e)
        {
            lock ("weee!")
            {
                sd["text"] = txtInput.Text;
            }
        }
    }
}