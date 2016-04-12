using System;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace Proxy
{
    class Program
    {
#if true
        const string IPADDRESS = "106.185.26.94";
        const int PORT = 9001;
        const int BACKPORT = 1090;
#else
        const string IPADDRESS = "";
        const int PORT = 0;
        const int BACKPORT = 0;
#endif

        public static SocksFactory socks;

        static void Main(string[] args)
        {
            socks = new SocksFactory();
            socks.address = IPADDRESS;
            socks.port = PORT;

            Thread factoryMainThread = new Thread(socks.run);
            factoryMainThread.Start();


            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, BACKPORT));
            tcpSocket.Listen(1024);
            tcpSocket.BeginAccept(new AsyncCallback(AcceptCallback), tcpSocket);

            Console.WriteLine("代理服务器地址 127.0.0.1:{0:d}", BACKPORT);
  
            Console.ReadKey();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            Socket sock = (Socket)ar.AsyncState;
            try
            {
                Socket conn = sock.EndAccept(ar);
                Socket remote = socks.getSocket();
                remote.BeginSend(new byte[] { 0, 1, 0, 1, 1234 / 256, 1234 % 256 }, 0, 6, 0, new AsyncCallback(remoteHandShake), new object[] { remote, conn});            
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            sock.BeginAccept(new AsyncCallback(AcceptCallback), sock);
        }

        public static void remoteHandShake(IAsyncResult ar)
        {
            object[] obj = ((object[])ar.AsyncState);
            Socket remote = (Socket)obj[0];
            Socket conn = (Socket)obj[1];

            try
            {
                remote.EndSend(ar);
                // 开始转发端口
                PortForward pf = new PortForward(conn, remote);
                pf.startForward();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
