import sh from 'shelljs'

const display = ':99'
sh.exec(`docker run --rm -e DISPLAY=${display} voice-relay:linux`)
