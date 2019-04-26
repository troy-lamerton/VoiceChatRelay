## microsoft/nanoserver is the smallest windows container
FROM microsoft/nanoserver
ARG NODE_VERSION=10.15.3
ENV NODE_VERSION=10.15.3

RUN powershell echo version${env:NODE_VERSION}
## add nodejs
ADD https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-win-x64.zip C:\\build\\node-v${NODE_VERSION}-win-x64.zip

RUN powershell -Command Expand-Archive C:\build\node-v${env:NODE_VERSION}-win-x64.zip C:\; Rename-Item C:\node-v${env:NODE_VERSION}-win-x64 node
RUN SETX PATH C:\node

## add built projects
# ADD <path/on/host> </path/in/container>
ADD . /VoiceRelayManager

## set environment variables for controller
ENV PORT_MANAGER 5000
# see BotContainer/Docker for PORT_CONTROLLER value
# this value is used by the relay manager when starting a new container
ENV PORT_CONTROLLER 3000
ENV APP_ID manager
ENV LOG_LEVEL debug

EXPOSE 5000
