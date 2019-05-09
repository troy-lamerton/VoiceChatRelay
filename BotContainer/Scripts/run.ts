import sh from 'shelljs'

// const display = ':99'
// sh.exec(`docker kill $(docker ps -q) & docker run --rm -e DISPLAY=${display} voice-relay:linux`)

const ports = `-p ${[6250, 5060, 5062, '12000-12010'].map(a => `${a}:${a}`).join(' -p ')}`
const command = `docker run --rm voice-relay:linux`

console.log(command)

sh.exec(command)
