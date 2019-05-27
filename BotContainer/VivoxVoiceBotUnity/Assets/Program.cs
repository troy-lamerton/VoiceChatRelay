using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using VivoxUnity;
using ConnectionState = VivoxUnity.ConnectionState;
using Debug = UnityEngine.Debug;

public class Program : MonoBehaviour {
    private static Program instance;
    public TextMeshPro debugText;
    public static float[] debugWave = new float[] {-1, -.8f , -.7f, -.6f, -0.5f, 0, 0.1f, .2f, .4f, .5f, .8f, 1, .8f, .7f, .6f, 0, -.3f, -.6f, -.9f, -1f};
    public static float[] debugWave2 = new float[] {-1, -.8f , -.7f, -.6f, -0.5f, 0, 0.1f, .2f, .4f, .5f, .8f, 1, .8f, .7f, .6f, 0, -.3f, -.6f, -.9f, -1f};

    public static Program I {
        get {
            if (instance == null) {
                throw new Exception("you fked up");
            }

            return instance;
        }
    }

    private void Awake() {
        instance = this;
    }

    #region Vivox state
    private IChannelSession channelSession;
    private Client client;
    private ILoginSession currentUser;

    public string ChannelId => channelSession?.AudioState == ConnectionState.Connected ? channelSession.Channel.Name : null;

    #endregion
    
    #region Inter process communication
    
    private UdpUser udpClient;
    private ControlPipeClient ControlPipe;
    private AudioProcessor audioProcessor;
    
    #endregion

    void OnEnable() {

        Config.VRelayPipePrefix = "vrelay";
        InitVivox();
        
        currentUser.PropertyChanged += (sender, change) => {
            if (change.PropertyName == nameof(ILoginSession.State) && currentUser.State == LoginState.LoggedIn) {
                // join channel after logging in
#if DEBUG || UNITY_ANDROID
                JoinChannelAsync(Globals.DebugGuild, Globals.DebugChannel);
#endif
            }
        };
        StartLoginAsync(currentUser);
        Task.Factory.StartNew(ReceiveFromDiscordBot);
    }

    private void InitVivox() {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject appContext = activity.Call<AndroidJavaObject>("getApplicationContext");
        AndroidJavaClass pluginClass = new AndroidJavaClass("com.vivox.vivoxnative.VivoxNative");
        pluginClass.CallStatic("init", appContext);
#endif
        audioProcessor = CreateAudioProcessor();
        client = audioProcessor.InitVivoxClient();
        currentUser = client.GetLoginSession(VivoxUtil.GetAccountId(Config.Username));
    }

    public void JoinChannelAsync(string guildId, string channelId) {
        Log.d("JoinChannel");
        var channelToJoin = new ChannelId(Config.TokenIssuer, Config.GetVoiceChannelId(guildId, channelId), Config.Domain);
        
        channelSession = currentUser.GetChannelSession(channelToJoin);
        
        channelSession.PropertyChanged += (sender, args) => {
            switch (args.PropertyName) {
                case nameof(channelSession.AudioState):
                    Log.i($"{args.PropertyName} -> {channelSession.AudioState}");
                    break;
                
                case nameof(channelSession.TextState):
                    Log.d($"{args.PropertyName} -> {channelSession.TextState}");
                    break;
                
                default:
                    Log.d($"{args.PropertyName} changed");
                    break;
            }
        };
        
        string joinToken = channelSession.GetConnectToken(Config.TokenKey, Config.TokenExpiration);
        channelSession.BeginConnect(true, true, TransmitPolicy.Yes, joinToken, res => {
            try {
                channelSession.EndConnect(res);
            } catch (Exception ex) {
                Log.e($"Failed to join {channelSession.Channel.Name} - {ex.Message}");
                currentUser.DeleteChannelSession(channelSession.Channel);
                channelSession = null;
                return;
            }
            
            sendSpeakingThrottled = new ThrottledAction<IEnumerable<IParticipant>>((participants) => {
                Task.Run(() => ControlPipe.SendSpeaking(participants));
            }, 2500);
            
            channelSession.Participants.AfterValueUpdated += SendVivoxPlayersSpeaking;
            
            channelSession.Participants.AfterKeyAdded += OnPlayerJoinEnableAudioProcesser;
            channelSession.Participants.BeforeKeyRemoved += OnBeforePlayerLeaveDisableAudioProcesser;
            OnPlayerJoinEnableAudioProcesser(null, null);

            Log.d("Channel is joined!");
        });
    }

    // enable or disable audio callbacks depending on if people are in the room
    private void OnBeforePlayerLeaveDisableAudioProcesser(object sender, KeyEventArg<string> e) {
        if (channelSession == null) return;
        // relaybot + person leaving + ?
        // disable if person leaving is the last human
        audioProcessor.Enabled = channelSession.Participants.Count - 1 >= 2;
    }

    private void OnPlayerJoinEnableAudioProcesser(object sender, KeyEventArg<string> e) {
        if (channelSession == null) return;
        audioProcessor.Enabled = channelSession.Participants.Count >= 2;
    }


    public void LeaveChannel() {
        Log.d("LeaveChannel");
        if (channelSession == null) return;
        
        channelSession.Participants.AfterValueUpdated -= SendVivoxPlayersSpeaking;
        sendSpeakingThrottled?.Dispose();
        channelSession.Disconnect();
        currentUser.DeleteChannelSession(channelSession.Channel);
        
        channelSession = null;
    }

    void OnDisable() {
        // stop all vivox stuff
        client?.Uninitialize();
    }

    private void StartLoginAsync(ILoginSession user) {
        client.Login(user, res => {
            try {
                // wait for result
                _ = ((AsyncResult<ILoginSession>) res).Result;
            } catch (Exception ex) {
                Log.wtf(ex, "Failed login overall");
            }
        });
    }

    /// <summary>
    /// Because the audio reaches clients after a longer delay,
    /// This method delays sending the players speaking message.
    /// So the players speaking message arrives at the vivox clients
    /// at roughly the same time as the audio data.
    /// </summary>
    /// <param name="discordNamesSpeaking">comma separated list of discord names that are speaking</param>
    public async Task UpdateDiscordSpeakers(string discordNamesSpeaking) {
        if (channelSession?.TextState != ConnectionState.Connected) return;
        if (string.IsNullOrEmpty(discordNamesSpeaking))
            throw new ArgumentException("Cannot send an empty message to vivox", nameof(discordNamesSpeaking));

        await Task.Delay(350);
        
        channelSession.BeginSendText(discordNamesSpeaking, ar => {
            try {
                channelSession.EndSendText(ar);
            } catch (Exception ex) {
                Log.wtf(ex);
            }
        });
    }

    private void SendVivoxPlayersSpeaking(object sender, ValueEventArg<string, IParticipant> arg) {
        if (channelSession.Participants == null) throw new NoNullAllowedException($"{nameof(channelSession)}.{nameof(channelSession.Participants)} is null! Cannot send v players speaking");
        sendSpeakingThrottled.Invoke(channelSession.Participants);
    }

    private ThrottledAction<IEnumerable<IParticipant>> sendSpeakingThrottled;

    private long samplesReceived = 0; // debugging

    private async Task ReceiveFromDiscordBot() {
        // UNCAUGHT EXCEPTIONS THROWN IN HERE ARE SWALLOWED

        // pipe commands
//        ControlPipe = new ControlPipeClient(Config.VRelayPipePrefix);
//        var success = await ControlPipe.Connect();
//        if (success) {
//            Log.d($"Control pipe connection is live.");
//        } else {
//            Log.w($"Control pipe not connected and will not try to reconnect.");
//        }

        // handshake with server
        udpClient.SendString("hello");
        try {
            while (true) {
                // wait for response from server
                var received = await udpClient.ReceiveAsync();
                byte[] data = received.Data;
                string resp = Encoding.ASCII.GetString(data);
                if (resp.StartsWith("HELLO")) {
                    // we successfully connected to the discord bot's server
                    Log.i("Connected to dbot for audio relay.");
                    break;
                }
#if DEBUG
                Log.w($"Expecting HELLO from server, not '{resp}'");
#endif
            }

            // receive audio from dbot
            while (true) {
                var sample = udpClient.Receive();
                samplesReceived++;
                audioProcessor.AddSample(sample);
            }
        } catch (Exception ex) {
            Log.wtf(ex);
        }
    }

    private AudioProcessor CreateAudioProcessor() {
        // open udp connection to the discord bot

        Log.d("Connecting to discord bot's udp server...");
        var port = int.Parse(Config.Get(Config.EnvVarName.DBOT_VOICE_PORT));
        udpClient = UdpUser.ConnectTo("127.0.0.1", port);

        return new AudioProcessor(udpClient);
    }

    private string debugStatus = "";
    private string channelStatus = "";
    
    private void Update() {
        Client.RunOnce();
        
        channelStatus = (channelSession == null ? "< no channel >" : $"{channelSession.Key.Name} - {channelSession.Participants.Count} players");
        var dbotConnection = "DBot: " + (udpClient.IsConnected ? "connected" : "< connecting >");
        var userString = "User: ";
        if (currentUser != null) {
            userString += currentUser.State.ToString();
            if (client?.LoginSessions.Count > 0) {
                var userSession = client.LoginSessions.First();
                if (userSession != null) {
                    userString += " " + userSession.Key.Name;
                }
            }
        } else {
            userString += "< null >";
        }
        debugStatus = $"{dbotConnection}\n{userString}\n{samplesReceived:e2} dbot samples/s";
        debugText.text = debugStatus;
        PlotWave(debugWave, 5f);
        PlotWave(debugWave2, -1.5f);
    }
    private void PlotWave(float[] samples, float yOffset) {
        var x = -16f;
        const float scale = 6f;
        var gap = Mathf.Abs(x) * 2 / samples.Length;
        foreach (var amp in samples) {
            x += gap;
            var start = new Vector3(x, yOffset + amp * scale, 0f);
            var end = new Vector3(start.x, start.y - 0.05f, start.z + 1f);
            Debug.DrawLine(start, end, Color.cyan);
        }
    }
    
}
