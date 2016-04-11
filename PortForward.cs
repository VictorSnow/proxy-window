using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Proxy
{
    class PortForward
    {
        private const int BUFFSIZE = 16384;

        public Socket local;
        public Socket remote;

        private byte[] buffLocal = new byte[BUFFSIZE];
        private byte[] buffRemote = new byte[BUFFSIZE];

        public PortForward(Socket local, Socket remote)
        {
            this.local = local;
            this.remote = remote;
        }

        public void startForward()
        {
            local.BeginReceive(buffLocal, 0, BUFFSIZE, 0, new AsyncCallback(LocalRecvCallback), null);
            remote.BeginReceive(buffRemote, 0, BUFFSIZE, 0, new AsyncCallback(RemoteRecvCallback), null);
        }

        ~PortForward()
        {
            Console.WriteLine("释放成功");
        }

        public void fallback(Exception e)
        {
            //Console.WriteLine(e.ToString());

            try
            {
                if(local != null)
                {
                    local.Close();
                    local = null;
                    buffLocal = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            try
            {
                if(remote != null)
                {
                    remote.Close();
                    remote = null;
                    buffRemote = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine("异常关闭");
        }

        public void LocalRecvCallback(IAsyncResult ar)
        {
            try
            {
                int recv = local.EndReceive(ar);
                if(recv > 0)
                {
                    byte[] dest = this.encoder(buffLocal, recv);
                    remote.BeginSend(dest, 0, recv, 0, new AsyncCallback(LocalSendCallback), null);
                    local.BeginReceive(buffLocal, 0, BUFFSIZE, 0, new AsyncCallback(LocalRecvCallback), null);
                    return;   
                }
            }
            catch (Exception ex)
            {
                fallback(ex);
            }
        }

        public void LocalSendCallback(IAsyncResult ar)
        {
            try
            {
                remote.EndSend(ar);
                return;
            }
            catch (Exception ex)
            {
                fallback(ex);
            }
        }

        public void RemoteRecvCallback(IAsyncResult ar)
        {
            try
            {
                int recv = remote.EndReceive(ar);
                if (recv > 0)
                {
                    byte[] dest = this.encoder(buffRemote, recv);
                    local.BeginSend(dest, 0, recv, 0, new AsyncCallback(RemoteSendCallback), null);
                    remote.BeginReceive(buffRemote, 0, BUFFSIZE, 0, new AsyncCallback(RemoteRecvCallback), null);
                    return;
                }
            }
            catch (Exception ex)
            {
                fallback(ex);
            }
        }

        public void RemoteSendCallback(IAsyncResult ar)
        {
            try
            {
                local.EndSend(ar);
                return;             
            }
            catch (SocketException ex)
            {
                fallback(ex);
            }
        }

        private unsafe byte[] encoder(byte[] str, int length)
        {
            byte[] dest = new byte[length];
            System.Buffer.BlockCopy(str, 0, dest, 0, length);
        
            if(length<=0)
            {
                return null;
            }

            fixed(byte* source= dest)
            {
                byte* p = source + length - 1;

                while(p>=source)
                {
                    *p ^= 96;
                    p--;
                }
            }
            return dest;
        }
    }
}
