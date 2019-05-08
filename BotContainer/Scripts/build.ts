import sh from 'shelljs'
import rimraf from 'rimraf'
import fs from 'fs'
import buildContainer from './helpers/build_container'

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
buildContainer()

function execute(command: string) {
    var res = sh.exec(command, {async: false})
    console.log(res.stdout)

    const err = res.code != 0
    if (err) {
        throw new Error(res.stderr)
    }
}