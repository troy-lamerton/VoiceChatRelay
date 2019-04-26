# escape=`
## microsoft/nanoserver is the smallest windows container
## but it doesnt have needed .dlls for the java discord bot to run

FROM microsoft/windowsservercore:latest

SHELL ["powershell", "-command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# TODO: gpg signature verification for SHASUMS256.txt
RUN curl 'https://nodejs.org/dist/latest-carbon/node-v8.15.1-x64.msi' -OutFile 'node.msi' -UseBasicParsing;

SHELL ["cmd", "/S", "/C"]

RUN msiexec /i C:\node.msi /quiet /qn /norestart /log C:\node-install.log && del C:\node.msi

## Add java package into container

SHELL ["powershell", "-command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

ENV JAVA_PKG=server-jre-8u202-windows-x64.tar.gz `
    JAVA_HOME=C:\\jdk1.8.0_202

ADD $JAVA_PKG /

RUN $env:Path += ';$env:JAVA_HOME\bin'

SHELL ["cmd", "/S", "/C"]

## Enable Remote desktop
EXPOSE 3389
RUN cscript C:\Windows\System32\Scregedit.wsf /ar 0
RUN cscript C:\Windows\System32\Scregedit.wsf /ar /v
# RUN reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp" /v LanAdapter /t REG_DWORD /d 0 /f

SHELL ["powershell", "-command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]
RUN (Get-ItemProperty -Path 'HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp' -Name LanAdapter).LanAdapter
## Install virtual audio cable - vivox needs an audio output device

# ADD VBCABLE_Setup.msi /

# RUN msiexec /i C:\VBCABLE_Setup.msi /quiet /qn /norestart /log C:\vbcable-install.log
# RUN type C:\vbcable-install.log

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
    DBOT_VOICE_PORT=9050 `
    DISCORD_LOGIN_TOKEN=NTUyNDk3MjI0Njk3OTA1MTg0.D3QuSw.LDA7IgupKRuYimrEA9h0sx6i9WQ

# WORKDIR /controller
# CMD node .

# inject Windows Audio Session API - only runs on Windows Server 2016 Core so I think this activates Windows Audio service
# AO -WASAPI

# RUN sc queryex type= service state= all | find /i "Windows"

# ADD SoundVolumeView.exe /debug/svv.exe
# WORKDIR /debug
# RUN svv.exe /scomma dump.csv
# RUN type dump.csv
