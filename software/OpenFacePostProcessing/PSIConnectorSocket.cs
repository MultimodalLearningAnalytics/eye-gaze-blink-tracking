// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

namespace PSIPostProcessing
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    public class PSIConnectorSocket
    {
        private Socket server;
        private Socket client;

        public PSIConnectorSocket(int port)
        {
            this.server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress hostIP = (Dns.Resolve(IPAddress.Any.ToString())).AddressList[0];
            IPEndPoint ep = new IPEndPoint(hostIP, port);
            this.server.Bind(ep);
            this.server.Listen(10); // queue of max. 10 connection
        }

        public void WaitForConnection()
        {
            this.client = this.server.Accept();
        }

        public void WaitForReady()
        {
            byte[] buffer = new byte[1];
            if (this.client.Receive(buffer) != 1)
            {
                throw new Exception("[PSIConnectorSocket] Did not receive any bytes!");
            }
        }

        public void SendComputeCommand()
        {
            this.client.Send(Encoding.ASCII.GetBytes("c"));
        }
    }
}
