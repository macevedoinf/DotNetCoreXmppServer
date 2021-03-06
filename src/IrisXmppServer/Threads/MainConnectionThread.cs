﻿using IrisXMPPServer.CoreClasses;
using IrisXMPPServer.Database;
using IrisXMPPServer.Helpers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace IrisXMPPServer.Threads
{
    public class MainConnectionThread : BaseThread
    {
        private TcpListener tcpListener;
        private ConnectionState connectionState;
        public static List<Client> clients = new List<Client>();
        private Database.DbHandler handler;

        public ConnectionState getConnectionState()
        {
            return connectionState;
        }

        public MainConnectionThread() : base()
        {
            connectionState = new ConnectionState();
            handler = new Database.DbHandler(Configuration.databaseEngine, Configuration.databaseHost, Configuration.databasePort, Configuration.databaseUser, Configuration.databasePassword, Configuration.databaseSchema);
        }

        private async Task<TcpClient> acceptTcpClient()
        {
            return (await tcpListener.AcceptTcpClientAsync());
        }

        public override void RunThread()
        {
            TcpClientStart();
        }

        public class ConnectionState
        {
            private bool started;

            public bool Started
            {
                get
                {
                    return started;
                }

                set
                {
                    started = value;
                }
            }

            public string Status
            {
                get
                {
                    return status;
                }

                set
                {
                    status = value;
                }
            }

            private string status;
        }

        private Task<IPAddress[]> getIpAdressess()
        {
            return Dns.GetHostAddressesAsync(Configuration.hostName);
        }

        private void TcpClientStart()
        {
            try
            {
                IPAddress[] ips = getIpAdressess().Result;
                foreach (IPAddress ip in ips) {
                    try
                    {
                        TcpListener serverSocket = new TcpListener(ip, Configuration.port);
                        TcpClient clientSocket = default(TcpClient);

                        serverSocket.Start();
                        tcpListener = serverSocket;
                        Console.WriteLine("=>DotNetXmppServer Started... Listening@" + ip.ToString() + ":" + Configuration.port);
                        this.connectionState = new ConnectionState();
                        connectionState.Started = true;
                        connectionState.Status = "OK";

                        while (true && this.connectionState.Started)
                        {
                            clientSocket = AcceptClient().Result;
                            handleClient client = new handleClient();
                            client.startClient(clientSocket, handler);
                        }

                        clientSocket.Dispose();
                        serverSocket.Stop();
                        Console.WriteLine("=>Server Offline");
                        break;
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("=>Trying to listen@" + ip + ":" + Configuration.port + " failed, trying new configuration");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        private Task<TcpClient> AcceptClient()
        {
            return tcpListener.AcceptTcpClientAsync();
        }

        //Class to handle each client request separatly
        private class handleClient
        {
            TcpClient clientSocket;
            DbHandler dbHandler;
            public void startClient(TcpClient inClientSocket, DbHandler dbHandler)
            {
                this.clientSocket = inClientSocket;
                this.dbHandler = dbHandler;
                Thread ctThread = new Thread(doChat);
                ctThread.Start();
            }

            private void doChat()
            {
                Client newClient = new Client(DateTime.Now.ToString("ddMMyyyyHHmmss") + new Random().Next(5000), clientSocket);
                Console.WriteLine("=>Client Id: " + newClient.InternalId + " Connected");
                newClient.Connected = true;
                clients.Add(newClient);
                               
                SocketHelper helper = new SocketHelper(newClient, dbHandler);

                while (newClient.Connected)
                {
                    helper.processMsg();
                }
            }
        }
    }
}
