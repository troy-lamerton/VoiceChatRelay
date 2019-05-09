import sh from 'shelljs'
import rimraf from 'rimraf'
import fs from 'fs'
import buildContainer from './helpers/build_container'
import path from 'path'
import { platform } from 'os'

const j = path.join
const vrelayPath = j('Docker', 'vivoxrelay')
// clean
rimraf.sync('Docker/discordbot/bin')
rimraf.sync('Docker/discordbot/lib')
rimraf.sync('Docker/controller')

if (platform() == "win32") {
    // clean vrelay
    rimraf.sync(vrelayPath)

    // build vrelay
    const folder = j('.', 'VivoxVoiceBot', 'VivoxVoiceRelayWindows')
    execute(`cd ${folder} && call build.bat`)
    const buildOutputFolder = j(folder, 'bin', 'x86', 'Release')
    sh.cp('-r', buildOutputFolder, vrelayPath)
}

if (!fs.existsSync(vrelayPath)) {
    console.error(platform() == "win32" ? 'Build or copy vrelay must have failed' : 'Get vivox relay C# build from a windows pc!')
    process.exit(1)
}

// build discord bot
// the code is built into Docker/discordbot
console.log('Building discord bot')
execute(`cd ${j('.', 'DiscordVoiceBot')} && ${j('.', 'gradlew')} buildAndCopyToDocker`)

// build controller and copy into Docker/controller
console.log('Building controller server')

execute(`cd ${j('.', 'controller')} && yarn && yarn build`)
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