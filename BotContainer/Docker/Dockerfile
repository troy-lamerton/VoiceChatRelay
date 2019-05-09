FROM rjrivero/lxde-vnc
USER 0
# FROM debian:stable-slim # the original base for crossover dockerfile
COPY vncpasswd /home/vnc/Documents/vncpasswd
ADD https://media.codeweavers.com/pub/crossover/cxlinux/demo/crossover_16.2.5-1.deb /crossover_16.2.5-1.deb

RUN export DEBIAN_FRONTEND=noninteractive && \
	dpkg --add-architecture i386 && \
	apt-get update && \
	apt-get install -y --no-install-recommends sudo python python-dbus python-gtk2 && \
	useradd -ms /bin/bash -G sudo crossover && \
	passwd -d crossover && \
	(dpkg -i /crossover_16.2.5-1.deb 2>/dev/null || apt-get install -f -y --no-install-recommends) && \
	rm -rf /crossover_16.2.5-1.deb /var/lib/apt/lists/*

# USER crossover
# WORKDIR /home/crossover
# ENV HOME /home/crossover

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

##
# Prepare for wine & winetricks install
# RUN apt-get update \
#     && apt-get install -y software-properties-common apt-transport-https wget cabextract \
#     && dpkg --add-architecture i386 \
#     && wget -qO - https://dl.winehq.org/wine-builds/winehq.key | apt-key add - \
#     && apt-add-repository https://dl.winehq.org/wine-builds/debian/ \
#     && apt-get update

# Fix wine dependencies and then install wine
# RUN apt-get remove winbind && apt-get install -qq winbind \
    # && apt-get update --fix-missing \
    # && apt-get install -qq --install-recommends xvfb winehq-staging

# Use winetricks to install .NET Framework 4.5
# WORKDIR /helpers
# RUN wget -q http://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks \
#     && chmod +x ./winetricks \
#     && ./winetricks --unattended dotnet45

# Installing dotnet45 switched the os to windows 2003, lets change to win 7 and fux it up
    # && ./winetricks win7 \

# RUN wget 'http://download.microsoft.com/download/9/5/A/95A9616B-7A37-4AF6-BC36-D6EA96C8DAAE/dotNetFx40_Full_x86_x64.exe' \
#     && wine dotNetFx40_Full_x86_x64.exe /q && wineserver -k \
#     && ./winetricks settings list | grep sound \
#     && ./winetricks sound=alsa \
#     && ./winetricks settings list | grep sound
##

# fake audio output device - not really working tho
# RUN apt-get update \
#     && apt-get -qq install pulseaudio
# RUN useradd -m troy
# USER troy
# RUN pulseaudio --start \
#     && sleep 4 \
#     && pacmd load-module module-null-sink sink_name=Virtual_Sink sink_properties=device.description=Virtual_Sink \
#     && pactl list short sinks
# USER 0

COPY asound.conf /etc/asound.conf

## set environment variables
# /discordbot/bin/discordbot \
ENV DBOT_PATH=/bin/bash \
    VRELAY_PATH=/vivoxrelay/run.sh \
    PORT_CONTROLLER=3000 \
    APP_ID=controller \
    LOG_LEVEL=info \
    VRELAY_PIPE_PREFIX=vrelay \
    DBOT_PIPE_PREFIX=dbot \
    DISCORD_LOGIN_TOKEN=NTUyNDk3MjI0Njk3OTA1MTg0.D3QuSw.LDA7IgupKRuYimrEA9h0sx6i9WQ \
    DISPLAY=:99

## Add built projects

COPY controller /controller
COPY discordbot /discordbot
COPY vivoxrelay /vivoxrelay
COPY run_vrelay.sh /vivoxrelay/run.sh

RUN chmod +x /discordbot/bin/discordbot \
    && chmod +x /vivoxrelay/run.sh

# WORKDIR /controller
# CMD node .
# USER vnc

# crossover instaleld net framework 4.5.1 into bottle Windows 7
# the crossover installer created the win7 bottle and then installed .net 2.0 and 4.0 into it