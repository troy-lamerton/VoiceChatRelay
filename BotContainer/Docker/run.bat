REM docker run --rm -it --entrypoint /bin/bash -p 5500:3000 voice-relay:linux
docker run --rm -it --link display:xserver --volumes-from display suchja/wine:latest /bin/bash