using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Proxy
{
    class SocketWrapper
    {
        const int BUFFER_SIZE = 8096;

        public delegate void onRecv(byte[] buffer, int length);
        public delegate void onEnd();

        public onRecv recvHandler;
        public onEnd endHandler;

        private Socket socket;
        private bool closed;
        private byte[] recvBuff;
        
        public SocketWrapper(Socket socket)
        {
            this.socket = socket;
            this.closed = false;

            recvBuff = new byte[BUFFER_SIZE];
        }

        public SocketWrapper attach(onRecv recvHandler, onEnd endHandler)
        {
            this.recvHandler += recvHandler;
            this.endHandler += endHandler;
            return this;
        }

        public void endSocket()
        {
            if(closed)
            {
                return;
            }
            closed = true;

            try
            {
                socket.Close();
            }
            catch(Exception)
            {
                
            }
            endHandler.Invoke();
        }

        public void send(byte[] buffer, int length)
        {
            if(closed)
            {
                return;
            }
            try
            {
                socket.BeginSend(buffer, 0, length, 0, new AsyncCallback(sendCallback), null);
            }
            catch (Exception)
            {
                endSocket();
            }
        }

        private void sendCallback(IAsyncResult ar)
        {
            if(closed)
            {
                return;
            }
            try
            {
                socket.EndSend(ar);
            }
            catch (Exception)
            {
                endSocket();
            }
        }

        public void recv()
        {
            if(closed)
            {
                return;
            }
            try
            {
                socket.BeginReceive(recvBuff, 0, BUFFER_SIZE, 0, new AsyncCallback(recvCallback), null);
            }
            catch(Exception)
            {
                endSocket();
            }
        }

        private void recvCallback(IAsyncResult ar)
        {
            if(closed)
            {
                return;
            }

            try
            {
                int recvLength = socket.EndReceive(ar);
                if (recvLength > 0)
                {
                    // copy recv buffer content to new buffer
                    byte[] decodeBuffer = encoder(recvBuff, recvLength);
                    recvHandler.Invoke(decodeBuffer, recvLength);

                    // loop
                    recv();
                }
                else
                {
                    endSocket();
                }
            }
            catch(Exception)
            {
                endSocket();
            }
        }

        private unsafe byte[] encoder(byte[] str, int length)
        {
            byte[] dest = new byte[length];
            System.Buffer.BlockCopy(str, 0, dest, 0, length);

            if (length <= 0)
            {
                return null;
            }

            fixed (byte* source = dest)
            {
                byte* p = source + length - 1;

                while (p >= source)
                {
                    *p ^= 96;
                    p--;
                }
            }
            return dest;
        }
    }
}
