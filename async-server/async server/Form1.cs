using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace async_server
{
    public partial class Form1 : Form
    {
        private ChatServer chatserver;

        private const int Port = 12345;
        public Form1()
        {
            InitializeComponent();
        }

        //private async void Form1_Load(object sender, EventArgs e)
        //{
        //    chatserver = new ChatServer(Port);
        //    chatserver.MessageReceived += AppendMessage;
        //    chatserver.ClientConnected += AppendMessage;
        //    chatserver.ClientDisconnected += AppendMessage;
        //}
        private void AppendMessage(string message) 
        {
            if(InvokeRequired) 
            {
                Invoke(new Action<string>(AppendMessage), message);
                return;
            }

            textBox1.AppendText(message + Environment.NewLine);
        }


        private async void button1_Click(object sender, EventArgs e)
        {
            chatserver = new ChatServer(Port);
            chatserver.MessageReceived += AppendMessage;
            chatserver.ClientConnected += AppendMessage;
            chatserver.ClientDisconnected += AppendMessage;
            await chatserver.StartAsync();
            
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
    public class ChatServer
    {
        private TcpListener listener;
        private ConcurrentDictionary<TcpClient, NetworkStream> clients;
        public event Action<string> MessageReceived;
        public event Action<string> ClientConnected;
        public event Action<string> ClientDisconnected;

        public ChatServer(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            clients = new ConcurrentDictionary<TcpClient, NetworkStream>();
        }

        public async Task StartAsync()
        {
            listener.Start();
            ClientConnected?.Invoke("Server started...");
            await AcceptClientsAsync();
        }

        private async Task AcceptClientsAsync()
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                ClientConnected?.Invoke("Client connected.");
                var stream = client.GetStream();
                clients[client] = stream;
                _ = HandleClientAsync(client, stream);
            }
        }

        private async Task HandleClientAsync(TcpClient client, NetworkStream stream)
        {
            var buffer = new byte[1024];
            while (client.Connected)
            {
                try
                {
                    var byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (byteCount == 0)
                        break;

                    var message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    MessageReceived?.Invoke($"Received: {message}");
                    await BroadcastMessageAsync(message, client);
                }
                catch
                {
                    break;
                }
            }

            stream.Close();
            clients.TryRemove(client, out _);
            ClientDisconnected?.Invoke("Client disconnected.");
        }

        private async Task BroadcastMessageAsync(string message, TcpClient senderClient)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            foreach (var kvp in clients)
            {
                var client = kvp.Key;
                if (client != senderClient)
                {
                    var stream = kvp.Value;
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                }
            }
        }
    }
}
