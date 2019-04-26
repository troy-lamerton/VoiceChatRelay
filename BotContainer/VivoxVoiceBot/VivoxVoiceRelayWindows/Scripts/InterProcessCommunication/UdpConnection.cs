using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

public struct Received {
    public IPEndPoint Sender;
    public byte[] Data;
}

/// <summary>
/// 1. Instantiate UdpUser
/// 2. Send something to server
/// 3. Call ReceiveAsync
/// 4. Udp client is ready to communicate with the server
/// </summary>
public abstract class UdpBase {
    protected IPEndPoint ServerIp;
    protected UdpClient Client;

    protected UdpBase() {
        Client = new UdpClient();
    }

    public async Task<Received> ReceiveAsync() {
        var result = await Client.ReceiveAsync();
        ServerIp = result.RemoteEndPoint;
        return new Received() {
            Sender = result.RemoteEndPoint,
            Data = result.Buffer,
        };
    }

    public void Shutdown() {
        Client.Dispose();
    }
}

// Client
public class UdpUser : UdpBase {
#if SAVE_MIC
    private byte[] debugSample = new byte[3840 * 50 * 7];
    private int debugIndex = 0;

    private void SaveDebugData() {
        File.WriteAllBytes("debugVoiceForDiscord.pcm", debugSample);
        Debug.Log("Saved debug audio");
    }
#endif

    public bool IsConnected => ServerIp != null;
    
    public static UdpUser ConnectTo(string hostname, int port) {
        var connection = new UdpUser();
        connection.Client.Connect(hostname, port);
        return connection;
    }

    public byte[] Receive() {
        return Client.Receive(ref ServerIp);
    }

    public void SendSync(byte[] data) {
        Client.Send(data, data.Length);
    }

    public async Task SendAsync(byte[] data) {
        await SendAsync(data, data.Length);
    }

    public async Task SendAsync(byte[] data, int length) {
#if SAVE_MIC
        if (debugSample == null) {
            return;
        }
        if (debugIndex + 1 > debugSample.Length) {
            SaveDebugData();
            debugSample = null;
            return;
        }
        Array.Copy(data, 0, debugSample, debugIndex, data.Length);
        debugIndex += data.Length;
#else
        await Client.SendAsync(data, length);
#endif
    }

    public void SendString(string message) {
        var datagram = Encoding.ASCII.GetBytes(message);
        Client.Send(datagram, datagram.Length);
    }
}
