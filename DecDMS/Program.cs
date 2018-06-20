using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DecDMS.Network.BaseNetwork;

namespace DecDMS
{
    class Test
    {
        public int count;

        public void plus()
        {
            count++;
        }
        public void minus()
        {
            count--;
        }

        public Test()
        {
            count = 0;
        }
    }

    class Program
    {
        private static Test test = new Test();

        private static ProxyServer Proxy;
        private static ProxyClient Client;

        public static async Task Main(string[] args)
        {
            // Create the token source.
            CancellationTokenSource cts1 = new CancellationTokenSource();
            CancellationTokenSource cts2 = new CancellationTokenSource();

            bool stop = false;

            while (!stop)
            {
                var lines = Console.ReadLine();
                String[] line = lines.Split(" ");
                switch (line[0])
                {
                    case "start":
                        ThreadPool.QueueUserWorkItem(new WaitCallback(StartServer), cts1.Token);
                        break;

                    case "stop":
                        cts1.Cancel();
                        Thread.Sleep(2500);
                        // Cancellation should have happened, so call Dispose.
                        cts1.Dispose();
                        break;
                    case "connect":
                        ThreadPool.QueueUserWorkItem(new WaitCallback(StartClient), cts2.Token);
                        break;

                    case "break":
                        cts2.Cancel();
                        Thread.Sleep(2500);
                        // Cancellation should have happened, so call Dispose.
                        cts2.Dispose();
                        break;

                    case "close":
                        stop = true;
                        break;

                    case "list":
                        foreach (KeyValuePair<IPAddress, int> client in Proxy.udpClients)
                            Console.WriteLine("Client: {0}:{1}", client.Key, client.Value);
                        break;
                    case "send":

                        Client.Send("Message " + line[1] + " " + line[2]);
                        break;
                    default:
                        break;
                }
            }
            Console.WriteLine("Finished");
            Console.ReadKey();
        }


        public static void StartServer(object obj)
        {
            CancellationToken token = (CancellationToken)obj;
            Console.WriteLine("Proxy starting");

            Proxy = new ProxyServer();
            while(true)
            {
                if(token.IsCancellationRequested)
                {
                    Proxy.Cancel();
                    Console.WriteLine("Proxy shutting down");
                    break;
                }
            }
        }

        public static void StartClient(object obj)
        {
            CancellationToken token = (CancellationToken)obj;
            Console.WriteLine("Client starting");


            //Client = new ProxyClient(IPAddress.Parse("159.69.18.11"));
            Client = new ProxyClient(IPAddress.Parse("192.168.1.66"));
            Client.RequestList();


            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    Client.Cancel();
                    Console.WriteLine("Client shutting down");
                    break;
                }
            }
        }
    }
}
