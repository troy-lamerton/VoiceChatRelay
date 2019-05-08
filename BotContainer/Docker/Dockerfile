FROM ubuntu:16.04

##
# The following are the ports and ranges used by Vivox:

# TCP: 80 and 443 - for web server (HTTP/HTTPS)
# UDP: 6250, 5060 and 5062 - for voice control signals (SIP)
# UDP: 12000 - 65000 - for voice media (RTP/RTCP)
##
EXPOSE 6250 5060 5062
EXPOSE 12000-65000

ENV DEBIAN_FRONTEND=noninteractive \
    WINEARCH=win32 \
    WINEPREFIX=/.wine32

# Install java
RUN apt-get update \
    && apt-get install -y curl openjdk-8-jre
ENV JAVA_HOME /usr/lib/jvm/java-8-openjdk-amd64

# Install nodejs
RUN curl -sL https://deb.nodesource.com/setup_11.x | bash \
    && curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - \
    && echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list \
    && apt-get update \
    && apt-get install -y nodejs yarn
    # && rm -rf /var/lib/apt/lists/*

# Prepare for wine & winetricks install
RUN apt-get update \
    && apt-get install -y software-properties-common apt-transport-https wget cabextract \
    && dpkg --add-architecture i386 \
    && wget -qO - https://dl.winehq.org/wine-builds/winehq.key | apt-key add - \
    && apt-add-repository "deb https://dl.winehq.org/wine-builds/ubuntu/ xenial main"

# Fix wine dependencies and then install wine
RUN apt-get remove winbind && apt-get install -qq winbind \
    && apt-get update --fix-missing \
    && apt-get install -qq --install-recommends xvfb wine-staging winehq-staging

# Use winetricks to install .NET Framework 4.5
WORKDIR /helpers
RUN wget -q http://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks \
    && chmod +x ./winetricks \
    && ./winetricks --unattended dotnet45

# dirty wine fixes

ENV DISPLAY :99

## Add built projects

COPY controller /controller
COPY discordbot /discordbot
COPY vivoxrelay /vivoxrelay
COPY run_vrelay.sh /vivoxrelay/run.sh

RUN chmod +x /discordbot/bin/discordbot \
    && chmod +x /vivoxrelay/run.sh

# fake audio output device - not really working tho
RUN apt-get update \
    && apt-get -qq install pulseaudio
RUN useradd -m troy
USER troy
RUN pulseaudio --start \
    && sleep 4 \
    && pacmd load-module module-null-sink sink_name=Virtual_Sink sink_properties=device.description=Virtual_Sink \
    && pactl list short sinks

## set environment variables
# /discordbot/bin/discordbot \
ENV DBOT_PATH=/bin/bash \
    VRELAY_PATH=/vivoxrelay/run.sh \
    PORT_CONTROLLER=3000 \
    APP_ID=controller \
    LOG_LEVEL=info \
    VRELAY_PIPE_PREFIX=vrelay \
    DBOT_PIPE_PREFIX=dbot \
    DISCORD_LOGIN_TOKEN=NTUyNDk3MjI0Njk3OTA1MTg0.D3QuSw.LDA7IgupKRuYimrEA9h0sx6i9WQ

USER 0

WORKDIR /controller
CMD node .
