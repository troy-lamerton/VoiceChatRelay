# Bot Container

## Why

The [Manager of Bot Containers](../ManagerOfBotContainers/README.md) needs to connect many Discord voice channels with Vivox channels.

One Bot Container connects the audio of one Discord voice channel with its matching Vivox channel.

## How

Tell it to join a Discord voice channel by id. It joins the Discord voice channel and the matching Vivox channel.

Players connect to Vivox and this project relays audio between Vivox and the Discord channel.

VivoxVoiceBot is a vivox client that listens to people talking in the Vivox channel. It also speaks audio data that it receives from the DiscordVoiceBot.

People in Discord see a single Discord bot user in the voice channel, regardless of how many players are speaking through it.

---

## Docker

Run one container per Discord voice channel. This project is designed to be built into a single container. With some changes it could be split into several containers.

The [Docker](./Docker) folder contains the Dockerfile and scripts to build the container from scratch.

---

## Controller
Server that runs named pipes to communicate with the other processes. This process spawns the other components as child processes.

This http server is the api used to control the container from outside.

Make GET and POST requests to tell it to join a voice channel and check the status of all components.

**Tech:**
Node web server, running 2x2 named pipe servers.

## DiscordVoiceBot
Discord bot which runs a UDP server to send discord's audio data to the Vivox bot. The UDP server also listens for audio data which it will immediately 'speak' in discord.

**Tech:** Kotlin project to be run in an environment with JRE installed. Running a UDP server for a single client. Running 2 named pipe clients.

## VivoxVoiceBot

Joins a Vivox voice channel and relays audio to and from Discordbotjava.

Listens on a named pipe for commands like 'join channel_51255001'.

**Tech:**
C# .NET project running Vivox unity sdk. Running a UDP client. Running 2 named pipe clients.

## vivox_unity_sdk

The Vivox Unity SDK modified to make use of the audio data callbacks. The modifications are rather small and could be done again on a newer version of the Vivox Unity SDK. The changes are to the Initialize(...) method in the C# project.