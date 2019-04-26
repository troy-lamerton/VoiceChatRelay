import sh, { ExecOutputReturnValue } from 'shelljs'
import l from '../common/logger'
import agent from 'superagent'

export async function getRunningContainersPool() {
    const pool = new ContainerPool()

    const containers = getRunningContainers()
    if (!containers) return pool
    
    pool.containerStatuses.push(...containers)
    pool.updateStatuses()

    return pool
}

function getRunningContainers(containerName: string = process.env.BOT_CONTAINER_NAME) {
    const res = sh.exec('docker ps', {silent: true, async: false}) as ExecOutputReturnValue
    if (res.code != 0) return

    return res.stdout.split('\n').slice(1, -1)
        .map(line => {
            const details = line.split(/\s{3,100}/)
            let port = details[5].match(/[^:]+:(\d{2,6})\-/)[1]
            return {
                image: details[1],
                address: details[5].match(/([^-]+)\:/)[1],
                hostPort: parseInt(port)
            }
        })
        .filter(container => container.image === containerName)
        .map(data => new ContainerStatus(data.hostPort, data.address))
}

export default class ContainerPool extends Array<ContainerStatus> {
    private containerName: string
    
    private nextPort: number = parseInt(process.env.FIRST_BOT_PORT) + 1

    constructor(containerName: string = process.env.BOT_CONTAINER_NAME) {
        super()
        this.containerName = containerName
    }

    get count() { return this.length }
    get idleCount() { return this.filter(bot => bot.idle).length }
    get containerStatuses() { return this }

    getBotInChannel = (id: string) => this.find(bot => bot.channel === id)
    
    async killAllBots() {
        // kill all bot containers
        const running = sh.exec(`docker ps -q`, {silent: true}).stdout
        sh.exec(`docker kill ${running.toString().replace(/\n/g, ' ')}`, {silent: true})
    }
    
    async runBotContainers(count: number) {
        const spawnedBots: ContainerStatus[] = []
        while (count > 0) {
            let run: ExecOutputReturnValue
            try {
                const bot = new ContainerStatus(this.getFreePort())
                run = sh.exec(`docker run --rm -d -p ${bot.controllerPort}:${process.env.PORT_CONTROLLER} ${this.containerName}`)
                if (run.stdout.includes('Error ')) {
                    throw new Error(run.stdout)
                }

                this.push(bot)
                spawnedBots.push(bot)
                l.debug(run.stdout)
                l.info(`Started bot at ${bot.controllerAddress}`)

            } catch (err) {
                l.error(err)
            }
            count--
        }
        return spawnedBots
        // TODO: use amazon
        // throw new Error('not implemented - spawning bot containers in production environment')
    }

    async getIdleOrRunContainer() {
        if (true) return new ContainerStatus(5500)
        for (const bot of this) {
            if (bot.idle) {
                return bot
            }
        }
        // there are no healthy bots running
        l.info(`Bot pool of ${this.length} has no idle bots. Running a new one.`)
        try {
            const newBots = await this.runBotContainers(1)
            return newBots[0]
        } catch (err) {
            l.error(err)
            return null
        }
    }

    // TODO: remove this , the containers should update a dynamo db table 'Containers' with their status
    async updateStatuses() {
        const infoPromises = this.map(status => agent.get(`${status.controllerAddress}/info`))
        const results = await Promise.all(infoPromises)
        results.filter(res => res !== undefined).forEach((res, i) => {
            const status = this[i]
            status.channel = res.body.channel
            status.idle = status.channel === '-1'
        })
    }

    private getFreePort(): number {
        this.nextPort++
        return this.nextPort
    }
}

export class ContainerStatus {
    public readonly controllerPort: number // port on the host to the container's controller
    public readonly address: string
    public idle: boolean = true
    public channel: string = '-1'

    public get controllerAddress() {
        // return `${this.address}:${this.controllerPort}`
        return `127.0.0.1:${this.controllerPort}`
    }

    constructor(port: number, address: string = '127.0.0.1') {
        this.controllerPort = port
        this.address = address
    }
}