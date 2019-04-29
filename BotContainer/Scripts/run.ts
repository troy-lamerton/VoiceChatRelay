import sh from 'shelljs'

// const display = '192.168.2.112:0'
const display = ':99'

sh.exec(`docker run --rm -e DISPLAY=${display} voice-relay:linux`)

/* then run in the container:

wine "/.wine/drive_c/Windows/System32/Notepad.exe"

*/