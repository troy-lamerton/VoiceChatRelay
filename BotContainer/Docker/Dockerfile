FROM node:10
ENV DEBIAN_FRONTEND noninteractive
RUN apt-get update \
  && apt-get install -y software-properties-common apt-transport-https \
  && dpkg --add-architecture i386 \
  && wget -nc https://dl.winehq.org/wine-builds/winehq.key \
  && apt-key add winehq.key \
  && apt-add-repository https://dl.winehq.org/wine-builds/debian/ \
  && apt-get update \
  && apt-get install -y --install-recommends winehq-stable xvfb \
  && apt-get remove -y software-properties-common apt-transport-https \
  && apt-get clean -y \
  && apt-get autoremove -y


ADD vivoxrelay /vivoxrelay

# ENV WINEPREFIX /.wine
ENV DISPLAY :99

CMD Xvfb $DISPLAY -nolisten tcp & wine cmd.exe

# CMD Xvfb $DISPLAY -nolisten tcp & wine /.wine/drive_c/Windows/System32/Notepad.exe