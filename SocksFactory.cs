using System;
using System.Collections.Generic;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


namespace Proxy
{
    class SocksFactory : IDisposable
    {
        // 需要预先连接的套接字地址和端口
        public string address;
        public int port;

        // 预先创建的套接字列表
        public List<Socket> lsocks = new List<Socket>();
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
                        for(int i = lsocks.Count-1; i>=0; i--)
                        {
                            Socket sock = lsocks[i];
                            // 发送心跳包
                            try
                            {
                                sock.BeginSend(new byte[] { 1, 1, 0, 0 }, 0, 4, 0, new AsyncCallback(heardbeatCallback), sock);
                            }
                            catch (Exception)
                            {
                                removeSocket(sock);
                            }
                        }
                    }
                    Console.WriteLine("发送心跳包");
                }
            }
        }

        public Socket getSocket()
        {
            getSem.WaitOne();
            lock (lsocks)
            {                
                if(lsocks.Count == 0)
                {
                    throw new Exception("同步异常");
                }
                Socket sock = lsocks[0];
                lsocks.Remove(sock);
                ConnectSem.Release();              
                return sock;
            }
        }

        public void removeSocket(Socket remote)
        {
            lock(lsocks)
            {
                lsocks.Remove(remote);
            }
            getSem.WaitOne();
            ConnectSem.Release();
        }
        
        public void tryConnect()
        {
            Console.WriteLine("尝试连接");
            // 连接到远程服务器
            try
            {
                Socket remote = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                remote.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                remote.BeginConnect(new IPEndPoint(IPAddress.Parse(address), port), new AsyncCallback(remoteConnectedCallback), remote);
            }
            catch(Exception)
            {
                ConnectSem.Release();
                Console.WriteLine("连接失败");
            } 
        }

        public void remoteConnectedCallback(IAsyncResult ar)
        {
            Socket remote = ((Socket)ar.AsyncState);

            try
            {
                remote.EndConnect(ar);
                lock(lsocks)
                {
                    lsocks.Add(remote);
                }
                getSem.Release();
                Console.WriteLine("加入新的链接");
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

        public void Dispose()
        {
            getSem.Close();
            ConnectSem.Close();

            foreach(Socket sock in lsocks)
            {
                try
                {
                    sock.Close();
                }catch(Exception)
                {

                }
            }
        }
    }
}
