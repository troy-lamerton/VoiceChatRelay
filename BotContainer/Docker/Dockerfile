FROM ubuntu:16.04

ENV DEBIAN_FRONTEND=noninteractive \
    WINEARCH=win32 \
    WINEPREFIX=/.wine32

# Install java and nodejs
RUN apt-get update \
    && apt-get install -y curl openjdk-8-jre
ENV JAVA_HOME /usr/lib/jvm/java-8-openjdk-amd64
RUN curl -sL https://deb.nodesource.com/setup_11.x | bash \
    && curl -sS https://dl.yarnpkg.com/debian/pubkey.gpg | apt-key add - \
    && echo "deb https://dl.yarnpkg.com/debian/ stable main" | tee /etc/apt/sources.list.d/yarn.list \
    && apt-get update \
    && apt-get install -y nodejs yarn \
    && rm -rf /var/lib/apt/lists/*

# Prepare for wine & winetricks install
RUN apt-get update \
    && apt-get install -y software-properties-common apt-transport-https wget cabextract \
    && dpkg --add-architecture i386 \
    && wget -qO - https://dl.winehq.org/wine-builds/winehq.key | apt-key add - \
    && apt-add-repository "deb https://dl.winehq.org/wine-builds/ubuntu/ xenial main"
# Fix wine (dependencies?) before install
RUN apt-get update --fix-missing
RUN apt-get install -qq --install-recommends xvfb wine-staging winehq-staging

# TODO: winetricks and dotnet 40
WORKDIR /helpers
RUN wget -q http://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks \
    && chmod +x ./winetricks \
    && ./winetricks --unattended dotnet45

ENV DISPLAY :99

# CMD Xvfb $DISPLAY -nolisten tcp & wine cmd.exe

## CMD Xvfb $DISPLAY -nolisten tcp & wine /.wine/drive_c/Windows/System32/Notepad.exe

## Add built projects

COPY controller /controller
COPY discordbot /discordbot
COPY vivoxrelay /vivoxrelay

RUN chmod +x /discordbot/bin/discordbot \
    && chmod +x /vivoxrelay/run.sh

## set environment variables
ENV DBOT_PATH=/discordbot/bin/discordbot \
    VRELAY_PATH=/vivoxrelay/run.sh \
    PORT_CONTROLLER=3000 \
    APP_ID=controller \
    LOG_LEVEL=info \
    VRELAY_PIPE_PREFIX=vrelay \
    DBOT_PIPE_PREFIX=dbot \
    DISCORD_LOGIN_TOKEN=NTUyNDk3MjI0Njk3OTA1MTg0.D3QuSw.LDA7IgupKRuYimrEA9h0sx6i9WQ

RUN wine --version
WORKDIR /controller
CMD node .
