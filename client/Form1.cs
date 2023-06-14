using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace client
{
    public partial class Form1 : Form
    {
        bool terminating = false;
        bool connected = false;
        bool manual_disconnect = false;

        Socket clientSocket;
        string myName = ""; //stores the name of the client

        public Form1()
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            InitializeComponent();
        }

        private void nameSend_button_Click(object sender, EventArgs e) //send the name of the client to the server
        {
            string message = textBox_name.Text;
            myName = message;

            if (message != "" && message.Length <= 64)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
                nameSend_button.Enabled = false; //after sending the name, disable the send button
            }
        }

        private void connectButton_Click_2(object sender, EventArgs e)
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            string IP = textBox_ip.Text;

            int portNum;
            if (Int32.TryParse(textBox_port.Text, out portNum))
            {
                try
                {
                    clientSocket.Connect(IP, portNum);
                    connectButton.Enabled = false;
                    disconnectButton.Enabled = true;
                    comboBox_place.Enabled = true;
                    nameSend_button.Enabled = true;
                    connected = true;

                    logs.AppendText("Connected to the server!\n"); //able to connect to the server

                    Thread receiveThread = new Thread(Receive);
                    receiveThread.Start();

                }
                catch //unable to connect to server
                {
                    logs.AppendText("Could not connect to the server!\n");
                }
            }
            else //wrong port
            {
                logs.AppendText("Check the port\n");
            }
        }

        private void Receive()
        {
            while (connected)
            {
                try
                {
                    Byte[] buffer = new Byte[256];
                    clientSocket.Receive(buffer);

                    string incomingMessage = Encoding.Default.GetString(buffer);
                    string incomingName = "";

                    if (incomingMessage != "")
                    {
                        //A started game continues and server sends a message in form of ClientName(X)s turn.
                        if (incomingMessage.Contains("'s turn") && !incomingMessage.Contains("A new game begins"))
                        {
                            incomingName = incomingMessage.Substring(0, incomingMessage.IndexOf("("));
                        }

                        //A new game is starting and the message coming from the server is not same as the in game messages.
                        else if (incomingMessage.Contains("A new game begins"))
                        {
                            incomingName = incomingMessage.Substring(incomingMessage.IndexOf("."), incomingMessage.IndexOf("("));
                        }

                        logs.AppendText(incomingMessage + "\n");

                        if ((incomingName.Contains(myName) && incomingName != "") || incomingMessage.Contains("Cell is already taken"))
                        {
                            sendButton.Enabled = true; //if the current client in control, then enable the send button for that client
                        }

                        else if (incomingMessage.Contains("Game is over!") || incomingMessage.Contains("It is a tie."))
                        {
                            connectButton.Enabled = false;
                            comboBox_place.Enabled = true;
                            sendButton.Enabled = false;
                        }

                        else if (incomingMessage.Contains("Server is full")) //If server is full, then the lastly joined client has to wait and try again after that.
                        {
                            connectButton.Enabled = true;
                            comboBox_place.Enabled = false;
                            sendButton.Enabled = false;
                            nameSend_button.Enabled = false;
                            connected = false;
                            clientSocket.Close();
                        }

                        else if (incomingMessage.Contains("This user name is taken"))
                        {
                            nameSend_button.Enabled = true; //Client is able to send his/her name again.
                        }

                        else if (incomingMessage.Contains("Somebody has left")) { } //nothing changes in the send button

                        else
                        {
                            sendButton.Enabled = false;
                        }

                    }
                }
                catch
                {
                    if (!terminating && manual_disconnect == false)
                    {
                        logs.AppendText("The server has disconnected\n");
                        connectButton.Enabled = true;
                        comboBox_place.Enabled = false;
                        sendButton.Enabled = false;
                    }

                    clientSocket.Close();
                    connected = false;
                }

            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            connected = false;
            terminating = true;
            Environment.Exit(0);
        }

        private void sendButton_Click(object sender, EventArgs e) //Client sends the place that he/she wants to put an X/O
        {
            sendButton.Enabled = false;
            string message = comboBox_place.Text;

            if (message != "" && message.Length <= 64)
            {
                Byte[] buffer = Encoding.Default.GetBytes(message);
                clientSocket.Send(buffer);
            }
        }

        private void disconnectButton_Click(object sender, EventArgs e) //This button is used for disconnect from the server without closing the environment.
        {
            logs.AppendText("You are disconnected.\n");
            clientSocket.Close();
            manual_disconnect = true;
            connectButton.Enabled = true;
            disconnectButton.Enabled = false;
        }
    }
}