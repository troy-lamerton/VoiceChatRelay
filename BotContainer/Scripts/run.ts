import sh from 'shelljs'

// const display = ':99'
// sh.exec(`docker kill $(docker ps -q) & docker run --rm -e DISPLAY=${display} voice-relay:linux`)

const ports = `-p ${[6080, 5901].map(a => `${a}:${a}`).join(' -p ')}`
const command = `docker run --name crossover-vnc ${ports} voice-relay:linux`

console.log(command)

sh.exec(command)
