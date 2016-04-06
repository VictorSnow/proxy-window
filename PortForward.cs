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
        public bool close = true;

        private byte[] buffLocal = new byte[BUFFSIZE];
        private byte[] buffRemote = new byte[BUFFSIZE];

        public PortForward(Socket local, Socket remote)
        {
            this.local = local;
            this.remote = remote;
            close = false;
        }

        public void startForward()
        {
            local.BeginReceive(buffLocal, 0, BUFFSIZE, 0, new AsyncCallback(LocalRecvCallback), null);
            remote.BeginReceive(buffRemote, 0, BUFFSIZE, 0, new AsyncCallback(RemoteRecvCallback), null);
        }

        ~PortForward()
        {
            //Console.WriteLine("释放成功");
        }

        public void fallback()
        {
            try
            {
                local.Close();
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }
            try
            {
                remote.Close();
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
            }

            local.Dispose();
            remote.Dispose();

            buffRemote = null;
            buffLocal = null;
        }

        public void LocalRecvCallback(IAsyncResult ar)
        {
            try
            {
                int recv = local.EndReceive(ar);
                if(recv > 0)
                {
                    this.encoder(buffLocal, recv);
                    remote.BeginSend(buffLocal, 0, recv, 0, new AsyncCallback(LocalSendCallback), null);
                }
                else
                {
                    fallback();
                }  
            }
            catch (Exception)
            {
                //Console.WriteLine(ex.ToString());
                fallback();
            }
        }

        public void LocalSendCallback(IAsyncResult ar)
        {
            try
            {
                remote.EndSend(ar);
                local.BeginReceive(buffLocal, 0, BUFFSIZE, 0, new AsyncCallback(LocalRecvCallback), null);
            }
            catch (Exception)
            {
                //Console.WriteLine(ex.ToString());
                fallback();
            }
        }

        public void RemoteRecvCallback(IAsyncResult ar)
        {
            try
            {
                int recv = remote.EndReceive(ar);
                if (recv > 0)
                {
                    this.encoder(buffRemote, recv);
                    local.BeginSend(buffRemote, 0, recv, 0, new AsyncCallback(RemoteSendCallback), null);
                }
                else
                {
                    fallback();
                }
            }
            catch (Exception)
            {
                //Console.WriteLine(ex.ToString());
                fallback();
            }
        }

        public void RemoteSendCallback(IAsyncResult ar)
        {
            try
            {
                local.EndSend(ar);
                remote.BeginReceive(buffRemote, 0, BUFFSIZE, 0, new AsyncCallback(RemoteRecvCallback), null);
            }
            catch (Exception)
            {
                //Console.WriteLine(ex.ToString());
                fallback();
            }
        }

        private unsafe void encoder(byte[] str, int length)
        {
            if(length<=0)
            {
                return;
            }

            fixed(byte* source= str)
            {
                byte* p = source + length - 1;

                while(p>=source)
                {
                    *p ^= 96;
                    p--;
                }
            }
        }
    }
}
