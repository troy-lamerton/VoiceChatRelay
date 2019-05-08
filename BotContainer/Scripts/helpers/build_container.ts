import sh from 'shelljs'

export default function() {
    console.log('Building docker container')
    execute(`cd ./Docker && docker build -t voice-relay:linux -f Dockerfile .`)
}

function execute(command: string) {
    var res = sh.exec(command, {async: false})
    console.log(res.stdout)

    const err = res.code != 0
    if (err) {
        throw new Error(res.stderr)
    }
}