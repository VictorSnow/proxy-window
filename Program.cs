using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    class Program
    {
        const string IPADDRESS = "*";
        const int PORT = 9001;
        const int BACKPORT = 1090;

        static void Main(string[] args)
        {
            Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            tcpSocket.Bind(new IPEndPoint(IPAddress.Any, BACKPORT));
            tcpSocket.Listen(1024);

            Console.WriteLine("代理服务器地址 127.0.0.1:{0:d}", BACKPORT);

            tcpSocket.BeginAccept(new AsyncCallback(AcceptCallback), tcpSocket);

            Console.ReadKey();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            Socket sock = (Socket)ar.AsyncState;
            try
            {
                Socket conn = sock.EndAccept(ar);

                // 连接到远程服务器
                Socket remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                remote.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                remote.BeginConnect(new IPEndPoint(IPAddress.Parse(IPADDRESS), PORT), new AsyncCallback(remoteConnectedCallback), new object[] { remote, conn });               
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            sock.BeginAccept(new AsyncCallback(AcceptCallback), sock);
        }

        public static void remoteConnectedCallback(IAsyncResult ar)
        {
            object[] obj= ((object[])ar.AsyncState);
            Socket remote = (Socket)obj[0];

            try
            {
                remote.EndConnect(ar);
                remote.BeginSend(new byte[] { 1, 1, 0, 0, 0, 1, 0, 1, 1234 / 256, 1234 % 256 }, 0, 10, 0,new AsyncCallback(remoteHandShake), obj);
            }catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
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
