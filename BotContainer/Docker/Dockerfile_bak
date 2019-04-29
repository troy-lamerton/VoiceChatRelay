# escape=`

FROM suchja/wine:latest

## TODO: Install node js

## TODO: Add java installation

## Add built projects

# ADD <relativepath/on/host> </path/in/container>
ADD controller /controller
ADD discordbot /discordbot
ADD vivoxrelay /vivoxrelay

## set environment variables
ENV DBOT_PATH=/discordbot/bin/discordbot.bat `
    VRELAY_PATH=/vivoxrelay/VivoxVoiceRelayWindows.exe `
    PORT_CONTROLLER=3000 `
    APP_ID=controller `
    LOG_LEVEL=info `
    VRELAY_PIPE_PREFIX=vrelay `
    DBOT_PIPE_PREFIX=dbot `
    DISCORD_LOGIN_TOKEN=NTUyNDk3MjI0Njk3OTA1MTg0.D3QuSw.LDA7IgupKRuYimrEA9h0sx6i9WQ

# WORKDIR /controller
# CMD node .