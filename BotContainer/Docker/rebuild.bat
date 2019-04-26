@echo off

if not exist discordbot\bin exit 10
if not exist discordbot\lib exit 11 
if not exist controller exit 12
if not exist vivoxrelay exit 13

echo.
echo Build docker image
echo.
docker build -t voice-relay:linux -f Dockerfile .
