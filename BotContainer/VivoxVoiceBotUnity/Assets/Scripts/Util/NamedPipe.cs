using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using VivoxUnity;

namespace Pipes {
    public class NamedPipeClient : IDisposable {
        private WritablePipeClient writablePipe;
        private ReadablePipeClient readablePipe;

        // pipe.IsConnected is blocking
        public bool IsConnected => readablePipe.IsConnected && writablePipe.IsConnected;

        protected NamedPipeClient(string pipesPrefix) {
            OnMessage += HandleMessage;
            CreatePipes(pipesPrefix);
        }

        private bool CreatePipes(string name) {
            try {
                readablePipe = new ReadablePipeClient(name + "_server", this);
                writablePipe = new WritablePipeClient(name + "_client", this);
                return true;
            } catch (Exception ex) {
                Log.e(ex);
            }

            return false;
        }

        public async Task<bool> Connect() {
            // Connect to the pipe
            try {
                Log.d($"connecting {readablePipe.Name}...");
                await readablePipe.Connect();
                Log.d($"connecting {writablePipe.Name}...");
                await writablePipe.Connect();
                return true;
            } catch (Exception ex) {
                Log.wtf(ex);
            }

            return false;
        }

        public async Task<bool> Send(Message msg) {
            return await writablePipe.Send(msg);
        }

        public async Task Ping() {
            await writablePipe.Ping();
        }

        internal Action<Message> OnMessage; // events sent between the readable and writable pipes

        protected virtual void HandleMessage(Message message) {
            Log.d(message.DebugString());
        }

        public void Dispose() {
            writablePipe?.Dispose();
            readablePipe?.Dispose();
        }
    }

    public class Message {
        public string command { get; }
        public string contents { get; }

        public Message(string command) {
            this.command = command;
        }

        public Message(string command, params string[] contents) : this(command) {
            this.contents = string.Join(",", contents);
        }

        private Message(string command, IEnumerable<string> contents) : this(command) {
            this.contents = string.Join(",", contents);
        }

        public static readonly Message Ping = new Message("ping");
        public static readonly Message Pong = new Message("pong");
        public static readonly Message Left = new Message("left");
        
        public static Message Info(string guildId, string channelId) => new Message("info", guildId ?? "-1", channelId ?? "-1");

        public static Message Speaking(IEnumerable<IParticipant> players) {
            var playersSpeaking = players.Where(player => player.SpeechDetected && !player.IsSelf).Select(player => player.Account.Name);
            return new Message("speaking", playersSpeaking);
        } 

        public override string ToString() {
            return $"{command};{contents}";
        }

        public string DebugString() {
            return $"[{command}]: ${contents}";
        }
    }

    internal abstract class NamedPipe : IDisposable {
        private const int RECONNECT_ATTEMPT_TIMEOUT = 2 * 1000;
        private const int RECONNECT_MAX_ATTEMPTS = 15 * 1000 / RECONNECT_ATTEMPT_TIMEOUT;

        public readonly string Name;
        protected readonly NamedPipeClient Client;
        protected readonly NamedPipeClientStream Pipe;
        public bool IsConnected => Pipe.IsConnected;

        protected NamedPipe(string name, NamedPipeClient client) {
            // TODO: move into readable and writable classes and use direction In OR Out
            Pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut);
            Name = name;
            Client = client;
        }

        // Throws on failure
        public async Task Connect() {
            for (int attempts = 0; attempts < RECONNECT_MAX_ATTEMPTS; attempts++) {
                if (!await ConnectPipe()) continue;
                OnConnected();
                return;
            }

            throw new Exception("Pipe failed connecting after many attempts");
        }

        private async Task<bool> ConnectPipe() {
            // Connect to the pipe
            try {
                Pipe.Connect(RECONNECT_ATTEMPT_TIMEOUT);
                return true;
            } catch (Exception ex) {
                Log.d(ex.StackTrace);
                await Task.Delay(RECONNECT_ATTEMPT_TIMEOUT);
            }

            return false;
        }

        protected abstract void OnConnected();
        protected abstract void TearDown();

        public void Dispose() {
            TearDown();
            Pipe?.Dispose();
        }
    }


    internal class WritablePipeClient : NamedPipe {
        private StreamWriter Writer;

        public async Task Ping() {
            await Send(Message.Ping);
        }

        async Task Pong() {
            await Send(Message.Pong);
        }

        public async Task<bool> Send(Message message) {
            if (Writer == null) return false;
//            Log.d($"[{Name}]: [{message.command}] {message.contents}");
            return await Write(message.ToString());
        }

        private async Task<bool> Write(string line, int retriesLeft = 1) {
            if (Writer == null) return false;

            try {
                Writer.WriteLine(line);
                return true;
            } catch (Exception ex) {
                // retry?
                if (retriesLeft <= 0) {
                    Log.e(ex);
                    return false;
                }
                Log.d($"Will retry in 10ms to send: {line}");
                await Task.Delay(10);
                return await Write(line, retriesLeft - 1);
            }
        }

        protected override void OnConnected() {
            Writer = new StreamWriter(Pipe) {AutoFlush = true};
        }

        protected override void TearDown() {
            Writer?.Dispose();
        }

        private void HandleMessage(Message msg) {
            switch (msg.command) {
                case "ping":
#pragma warning disable 4014
                    Pong();
#pragma warning restore 4014
                    break;
            }
        }

        public WritablePipeClient(string name, NamedPipeClient client) : base(name, client) {
            Client.OnMessage += HandleMessage;
        }
    }

    internal class ReadablePipeClient : NamedPipe {
        private readonly CancellationTokenSource runningTasks = new CancellationTokenSource();
        private Task readerTask;

        protected override void OnConnected() {
            var readerCancelToken = runningTasks.Token;
            // start reading pipe
            readerTask = Task.Factory.StartNew(ReadForever, readerCancelToken);
        }

        protected override void TearDown() {
            runningTasks.Cancel();
            if (readerTask?.IsFinished() == true) {
                readerTask.Dispose();
            }
        }

        private async void ReadForever() {
            StreamReader reader = new StreamReader(Pipe);

            while (!readerTask.IsCanceled) {
                try {
                    // read messages
                    var line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line)) {
                        // disconnect
                        break;
                    }

                    var parts = line.Split(';');
                    var command = parts[0];
                    var contents = (parts.Length > 1) ? parts[1] : "";
                    var message = new Message(command, contents);
                    Client.OnMessage.Invoke(message);
                } catch (IOException ex) {
                    if (!IsConnected) {
                        Log.wtf(ex, "Crashing pipe because of the following IOException");
                        throw;
                    }
                } catch (Exception ex) {
                    Log.e(ex);
                }

                await Task.Delay(10);
            }

            Log.w("Pipe server disconnected, stopping app");
            TearDown();
            Environment.Exit(Globals.ExitCode.PipeDisconnected);
        }

        public ReadablePipeClient(string name, NamedPipeClient client) : base(name, client) {
        }
    }
}
