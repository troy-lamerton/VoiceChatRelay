using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pipes;
using VivoxUnity;

public class ControlPipeClient : NamedPipeClient {
    public ControlPipeClient(string pipesPrefix) : base(pipesPrefix) {
    }

    public async Task SendSpeaking(IEnumerable<IParticipant> players) {
        await Send(Message.Speaking(players));
    }

    protected override void HandleMessage(Message message) {
        var parts = message.contents.Split(',');
        
        switch (message.command) {
            case "join":
                // example contents: "51444589267,51444007309"
                var guildId = parts[0];
                var channelId = parts[1];

//                GameController.I.LeaveChannel();
                Program.I.JoinChannelAsync(guildId, channelId);
                break;
            
            case "leave":
                Program.I.LeaveChannel();
                Send(Message.Left).Wait();
                break;
            
            case "speaking":
                // example contents: "troy,james1"
                Task.Run(() => Program.I.UpdateDiscordSpeakers(message.contents));
                break;
            
            case "info":
                var fullChannelName = Program.I.ChannelId;
                if (fullChannelName == null) {
                    Send(Message.Info(null, null)).Wait();
                    break;
                }

                string[] guildAndChannel = fullChannelName.Split('_');
                Send(Message.Info(guildAndChannel[0], guildAndChannel[1])).Wait();
                break;
            
            default:
                // do nothing
                break;
        }
    }
}
