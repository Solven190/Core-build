using Ryujinx.Common.Logging;
using Ryujinx.HLE.Debugger.Gdb;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Ryujinx.HLE.Debugger
{
    public partial class Debugger
    {
        private void DebuggerThreadMain()
        {
            IPEndPoint endpoint = new(IPAddress.Any, GdbStubPort);
            _listenerSocket = new TcpListener(endpoint);
            _listenerSocket.Start();
            Logger.Notice.Print(LogClass.GdbStub, $"Currently waiting on {endpoint} for GDB client");

            while (!_shuttingDown)
            {
                try
                {
                    _clientSocket = _listenerSocket.AcceptSocket();
                }
                catch (SocketException)
                {
                    return;
                }

                // If the user connects before the application is running, wait for the application to start.
                int retries = 10;
                while ((DebugProcess == null || GetThreads().Length == 0) && retries-- > 0)
                {
                    Thread.Sleep(200);
                }

                if (DebugProcess == null || GetThreads().Length == 0)
                {
                    Logger.Warning?.Print(LogClass.GdbStub,
                        "Application is not running, cannot accept GDB client connection");
                    _clientSocket.Close();
                    continue;
                }

                _clientSocket.NoDelay = true;
                _readStream = new NetworkStream(_clientSocket, System.IO.FileAccess.Read);
                _writeStream = new NetworkStream(_clientSocket, System.IO.FileAccess.Write);
                _commands = new GdbCommands(_listenerSocket, _clientSocket, _readStream, _writeStream, this);
                _commandProcessor = _commands.CreateProcessor();

                Logger.Notice.Print(LogClass.GdbStub, "GDB client connected");

                while (true)
                {
                    try
                    {
                        switch (_readStream.ReadByte())
                        {
                            case -1:
                                goto EndOfLoop;
                            case '+':
                                continue;
                            case '-':
                                Logger.Notice.Print(LogClass.GdbStub, "NACK received!");
                                continue;
                            case '\x03':
                                _messages.Add(new BreakInMessage());
                                break;
                            case '$':
                                string cmd = "";
                                while (true)
                                {
                                    int x = _readStream.ReadByte();
                                    if (x == -1)
                                        goto EndOfLoop;
                                    if (x == '#')
                                        break;
                                    cmd += (char)x;
                                }

                                string checksum = $"{(char)_readStream.ReadByte()}{(char)_readStream.ReadByte()}";
                                if (checksum == $"{Helpers.CalculateChecksum(cmd):x2}")
                                {
                                    _messages.Add(new CommandMessage(cmd));
                                }
                                else
                                {
                                    _messages.Add(new SendNackMessage());
                                }

                                break;
                        }
                    }
                    catch (IOException)
                    {
                        goto EndOfLoop;
                    }
                }

                EndOfLoop:
                Logger.Notice.Print(LogClass.GdbStub, "GDB client lost connection");
                _readStream.Close();
                _readStream = null;
                _writeStream.Close();
                _writeStream = null;
                _clientSocket.Close();
                _clientSocket = null;
                _commandProcessor = null;
                _commands = null;

                BreakpointManager.ClearAll();
            }
        }
    }
}
