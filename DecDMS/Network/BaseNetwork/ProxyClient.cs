using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DecDMS.Network.BaseNetwork
{
    public class ProxyClient
    {
        #region Свойства
        private static int SERVERUDPPORT = 10024;
        private static int SERVERTCPPORT = 10025;
        private static string FILESLISTCOMMAND = "list";
        private static string PUSHCOMMAND = "push";

        private static ProxyClient instance;
        private static object syncRoot = new object();

        private CancellationTokenSource cancelTokenSource; // для остановки всех процессов
        private IPAddress serverIp; // ip-адрес сервера
        private UdpClient udpClient; // udp-клиент
        private Status connectionStatus = Status.NoConnection; // состояние подключения к серверу
        #endregion

        #region Конструкторы
        public ProxyClient(IPAddress ip)
        {
            this.udpClient = new UdpClient();
            this.cancelTokenSource = new CancellationTokenSource();
            this.serverIp = ip;

            // проверка связи до сервера
            Task.Factory.StartNew(() => this.CheckConnection());
        }
        #endregion

        #region Методы
        ///<summary>
        /// Прослушка интерфейса по протоколу UDP
        ///</summary>
        ///<param name="client">UDP client</param>
        private void ListenUDP(CancellationToken cancelToken, UdpClient client)
        {
            try
            {
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    IPEndPoint ipep = null;
                    byte[] messageBytes = client.Receive(ref ipep);

                    IPAddress senderIp = ipep.Address; // ip отправителя

                    string message = Encoding.UTF8.GetString(messageBytes);

                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        if (this.OnMessageReceived != null)
                            this.OnMessageReceived.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException) { }

            catch (SocketException ex)
            {
                Console.WriteLine(string.Format("{0:hh:mm:ss} NetService.Listen: {1}", DateTime.Now, ex.Message));
            }
            finally
            {
                if (client != null)
                    client.Close();
            }
        }

        ///<summary>
        /// Проверка связи до сервера
        ///</summary>
        private void CheckConnection()
        {
            Ping ping = new Ping();

            while (true)
            {
                Thread.Sleep(5000);

                if (this.serverIp == null)
                {
                    Console.WriteLine("Setting up remote IP..");
                    continue;
                }

                try
                {
                    PingReply pr = ping.Send(this.serverIp);

                    if (pr.Status == IPStatus.Success)
                    {
                        if (this.connectionStatus != Status.Connected)
                        {
                            this.connectionStatus = Status.Connected;
                            Console.WriteLine("Connected");
                            //this.OnStatusChanged.Invoke(new StatusChangedEventArgs(this.connectionStatus));
                        }
                    }
                    else
                    {
                        if (this.connectionStatus != Status.NoConnection)
                        {
                            this.connectionStatus = Status.NoConnection;

                            //this.OnStatusChanged.Invoke(new StatusChangedEventArgs(this.connectionStatus));
                        }
                    }
                }
                catch (PingException)
                {
                    if (this.connectionStatus != Status.NoConnection)
                    {
                        this.connectionStatus = Status.NoConnection;

                        //this.OnStatusChanged.Invoke(new StatusChangedEventArgs(this.connectionStatus));
                    }
                }
            }
        }

        ///<summary>
        /// Отправка UDP PUSH сообщений серверу для поддержки связи
        ///</summary>
        private void HoldUDPConnection()
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(PUSHCOMMAND);

            while (true)
            {
                Thread.Sleep(20000);

                if (udpClient.Client == null)
                    break; // соединение разорвано

                IPEndPoint ipep = new IPEndPoint(this.serverIp, SERVERUDPPORT);

                this.udpClient.Send(messageBytes, messageBytes.Length, ipep);
            }
        }
        #endregion

        #region Общие свойства
        ///<summary>
        /// NetService Instance
        ///</summary>
        /*
        public static ProxyClient Instance
        {
            get
            {
                if (instance == null)
                    lock (syncRoot)
                        if (instance == null)
                            instance = new ProxyClient();

                return instance;
            }
        }
        */
        ///<summary>
        /// IP-адрес сервера
        ///</summary>
        public IPAddress ServerIp { get { return this.serverIp; } }

        public delegate void NetMessageReceivedEventHandler(string message);

        ///<summary>
        /// Возникает при получении сообщения
        ///</summary>
        public event NetMessageReceivedEventHandler OnMessageReceived;

        public delegate void NetFileReceivedEventHandler(string filePath);

        ///<summary>
        /// Возникает при получении файла
        ///</summary>
        public event NetFileReceivedEventHandler OnFileReceived;

        public delegate void StatusChangedEventHandler(StatusChangedEventArgs e);

        ///<summary>
        /// При изменении статуса
        ///</summary>
        public event StatusChangedEventHandler OnStatusChanged;
        #endregion

        #region Общие методы
        ///<summary>
        /// Устанавливает IP-адрес сервера
        ///</summary>
        ///<param name="ip">IP-адрес</param>
        public void SetServerIp(IPAddress ip)
        {
            this.udpClient.Close();

            this.connectionStatus = Status.NoConnection;
            this.OnStatusChanged.Invoke(new StatusChangedEventArgs(this.connectionStatus));

            this.serverIp = ip;

            this.udpClient = new UdpClient();
        }

        ///<summary>
        /// Запрос списка файлов
        ///</summary>
        public void RequestList()
        {
            if (this.serverIp == null)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0:hh:mm:ss} NetService.RequestList: не установлен IP-адрес сервера", DateTime.Now));

                return;
            }

            IPEndPoint ipep = new IPEndPoint(this.serverIp, SERVERUDPPORT);

            // если это первое сообщение, запустим сначала прослушку портов
            if (this.udpClient.Client == null)
                this.udpClient = new UdpClient();

            if (this.udpClient.Client.LocalEndPoint == null)
            {
                byte[] pushCmdBytes = Encoding.UTF8.GetBytes(PUSHCOMMAND);

                this.udpClient.Send(pushCmdBytes, pushCmdBytes.Length, ipep);

                Task.Factory.StartNew(() => this.ListenUDP(this.cancelTokenSource.Token, this.udpClient));
                Task.Factory.StartNew(() => this.HoldUDPConnection());
            }

            byte[] messageBytes = Encoding.UTF8.GetBytes(FILESLISTCOMMAND);

            this.udpClient.Send(messageBytes, messageBytes.Length, ipep);
        }

        ///<summary>
        /// Остановить все процессы прослушки интерфейсов
        ///</summary>
        public void Cancel()
        {
            if (this.cancelTokenSource != null)
                this.cancelTokenSource.Cancel();
        }
        #endregion

        #region OnStatusChanged event
        public class StatusChangedEventArgs
        {
            ///<summary>
            /// Статус подключения
            ///</summary>
            public Status Status { get; set; }

            public StatusChangedEventArgs(Status status)
            {
                this.Status = status;
            }
        }

        public enum Status
        {
            NoConnection,
            Connected
        }
        #endregion
    }
}
