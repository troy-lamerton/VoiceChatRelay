import sh from 'shelljs'
import rimraf from 'rimraf'
import fs from 'fs'

// clean
rimraf.sync('./Docker/discordbot/bin')
rimraf.sync('./Docker/discordbot/lib')
rimraf.sync('./Docker/controller')
// rimraf.sync('Docker/vivoxrelay') // NOTE: dont clean because this is built on a windows pc


// TODO: get vrelay build from windows pc
if (!fs.existsSync('Docker/vivoxrelay')) {
    console.error('Get vivox relay C# build from a windows pc!')
    process.exit(1)
}

// build discord bot
// the code is built into Docker/discordbot
console.log('Building discord bot')
execute('cd ./DiscordVoiceBot && ./gradlew buildAndCopyToDocker')

// build controller and copy into Docker/controller
console.log('Building controller server')

execute('cd ./controller && yarn && yarn build')
sh.cp('-r', './Controller/build', './Docker/controller')

// finally build the container
console.log('Building docker container')
execute(`DISPLAY=192.168.2.112:0 cd ./Docker && docker build -t voice-relay:linux -f Dockerfile .`)

function execute(command: string) {
    var res = sh.exec(command, {async: false})
    console.log(res.stdout)

    const err = res.code != 0
    if (err) {
        throw new Error(res.stderr)
    }
}