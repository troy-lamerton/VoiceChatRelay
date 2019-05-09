using System;
using System.Diagnostics;

public static class Config {
    // vivox server
    public static readonly Uri Server = new Uri("https://vdx5.www.vivox.com/api2");
    public static readonly string Domain = "vdx5.vivox.com";
    public static readonly string TokenIssuer = "troydemo-ddrelay-dev";
    public static readonly string TokenKey = "Ni5Y1cpWfpDqZ0IQusS22Dy8tkpaF0AD";
    public static readonly TimeSpan TokenExpiration = TimeSpan.FromMinutes(2);
    
    // vivox other
    public static readonly string Username = "relaybot";
    public static string VRelayPipePrefix;
    
    public static string Get(EnvVarName envVarName) {
        var value = Environment.GetEnvironmentVariable(envVarName.ToString());
        if (!string.IsNullOrEmpty(value)) {
            return value;
        }
        value = null;

        switch (envVarName) {
            case EnvVarName.DBOT_VOICE_PORT:
                value = "9050";
                break;
        }
        
        return value;
    }

    public static string GetVoiceChannelId(string discordGuildId, string discordChannelId) {
        return $"{discordGuildId}_{discordChannelId}";
    }

    public enum EnvVarName {
        DBOT_VOICE_PORT,
    }
}
