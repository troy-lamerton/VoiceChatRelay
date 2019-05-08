import sh from 'shelljs'

const display = ':99'
sh.exec(`docker kill $(docker ps -q) & docker run --rm -e DISPLAY=${display} voice-relay:linux`)
