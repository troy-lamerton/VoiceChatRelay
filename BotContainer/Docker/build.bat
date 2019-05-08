@ECHO off

SET DockerFolder=%CD%

REM ## clean
IF EXIST discordbot\bin rmdir /S /Q discordbot\bin
IF EXIST discordbot\lib rmdir /S /Q discordbot\lib
IF EXIST controller rmdir /S /Q controller
IF EXIST vivoxrelay rmdir /S /Q vivoxrelay

REM ## build vivox relay
ECHO Building vivox relay C# proj
ECHO.

SET VRelay=..\VivoxVoiceBot\VivoxVoiceRelayWindows
IF EXIST %VRelay%\bin\Release rmdir /S /Q %VRelay%\bin\Release
CD %VRelay%
CALL build.bat
CD %DockerFolder%
REM ## copy into Docker/vivoxrelay
IF NOT EXIST %VRelay%\bin\Release (ECHO Voice relay build failed! && EXIT 1)
XCOPY /S /I /Q %VRelay%\bin\Release .\vivoxrelay

REM ## build discord bot
REM ## the code is built into Docker/discordbot
CD ../DiscordVoiceBot
ECHO.
ECHO Building discord bot
CALL .\gradlew.bat buildAndCopyToDocker
CD %DockerFolder%

REM ## build controller and copy into Docker/controller
ECHO.
ECHO Building controller server
CD ../controller
CALL yarn install
CALL yarn build
CD %DockerFolder%
XCOPY /S /I /Q ..\controller\build .\controller

REM ## finally build the container
ECHO.
ECHO Building docker container
ECHO.
docker build -t voice-relay:linux -f Dockerfile .
