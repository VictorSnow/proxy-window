using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proxy
{
    class SocksFactory
    {
        // 需要预先连接的套接字地址和端口
        public string address;
        public int port;

        // 预先创建的套接字列表
        public LinkedList<Socket> lsocks = new LinkedList<Socket>();
        // 上一次的心跳包
        public double lastHeartBeat = 0;
        // 当前Socket列表可用的的信号量
        public Semaphore getSem = new Semaphore(0, 20);
        // 当前需要创建的套接字信号量
        public Semaphore ConnectSem = new Semaphore(20, 20);

        public double ToTimestamp(DateTime value)
        {
            TimeSpan span = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
            return (double)span.TotalSeconds;
        }

        public void run()
        {
            while(true)
            {
                while (ConnectSem.WaitOne(5000))
                {
                    tryConnect();
                }

                double now = ToTimestamp(DateTime.Now);
                if (now - lastHeartBeat > 10)
                {
                    lastHeartBeat = now;
                    lock (lsocks)
                    {
                        foreach (Socket sock in lsocks)
                        {
                            // 发送心跳包
                            sock.BeginSend(new byte[] { 1, 1, 0, 0 }, 0, 4, 0, new AsyncCallback(heardbeatCallback), sock);
                        }
                    }
                }
            }
        }

        public Socket getSocket()
        {
            lock(lsocks)
            {
                getSem.WaitOne();
                Socket sock = lsocks.First.Value;
                lsocks.RemoveFirst();
                ConnectSem.Release();              
                return sock;
            }
        }

        public void removeSocket(Socket remote)
        {
            lock(lsocks)
            {
                lsocks.Remove(remote);
                ConnectSem.Release();
            }
        }
        
        public void tryConnect()
        {
            Console.WriteLine("尝试连接");
            // 连接到远程服务器
            Socket remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            remote.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            remote.BeginConnect(new IPEndPoint(IPAddress.Parse(address), port), new AsyncCallback(remoteConnectedCallback), remote);
        }

        public void remoteConnectedCallback(IAsyncResult ar)
        {
            Socket remote = ((Socket)ar.AsyncState);

            try
            {
                remote.EndConnect(ar);
                lock(lsocks)
                {
                    lsocks.AddLast(remote);
                    getSem.Release();
                    Console.WriteLine("加入新的链接");
                }
            }
            catch (Exception)
            {
                ConnectSem.Release();
                Console.WriteLine("连接失败");
            }
        }

        public void heardbeatCallback(IAsyncResult ar)
        {
            
            Socket remote = (Socket)ar.AsyncState;

            try
            {
                remote.EndSend(ar);
            }
            catch (Exception)
            {
                removeSocket(remote);
                Console.WriteLine("心跳失败");
            }
        }
    }
}
