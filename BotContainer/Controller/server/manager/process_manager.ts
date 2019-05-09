import shell from 'shelljs'
import path from 'path'
import child_process, { ChildProcess } from 'child_process'
import l, { intArrayToString } from '../common/logger';
import ControlPipe from '../pipers/control_pipe';

export default class AliveProcessManager {
    private nextCheckToken: NodeJS.Timeout
    private stopping = false
    
    private dcommander: ControlPipe
    private vcommander: ControlPipe
    private dbot: ChildProcess
    private vrelay: ChildProcess
    
    private discordBotStatus = ProcessStatus.Stopped
    private vivoxRelayStatus = ProcessStatus.Stopped

    constructor(dcommander: ControlPipe, vcommander: ControlPipe) {
        this.dcommander = dcommander
        this.vcommander = vcommander
    }

    start() {
        this.stopping = false
        this.keepAliveLoop()
    }

    /**
     * @method killall Kill all spawned processes
     */
    killall() {
        this.stop()
        this.dbot && this.dbot.kill('SIGKILL')
        this.vrelay && this.vrelay.kill('SIGKILL')
    }

    /**
     * @method stop Stops keeping processes alive
     * Note that pings in progress will not be cancelled
     */
    private stop() {
        l.info('stopping keep alive')
        this.stopping = true
        clearTimeout(this.nextCheckToken)
    }

    spawnProcess(name: 'dbot' | 'vrelay') {
        if (process.env.DONT_SPAWN) return;
        if (name === 'dbot') {
            // start discord bot and pass it the pipe prefix to use
            this.startProcess('dbot')
            this.dbot.stdout.on('data', this.dbotLog);
            this.dbot.stderr.on('data', this.dbotLog);
            this.dbot.on('exit', (code) => {
                this.discordBotStatus = ProcessStatus.Stopped
                l.dbot(`DBot process exited with code ${code}`)
            });
            
        } else if (name === 'vrelay') {
            
            // start vivox relay and pass it the pipe prefix to use
            this.startProcess('vrelay')
            
            this.vrelay.stdout.on('data', this.vrelayLog);
            this.vrelay.stderr.on('data', this.vrelayLog);
            this.vrelay.on('exit', (code) => {
                this.vivoxRelayStatus = ProcessStatus.Stopped
                l.vrelay(`VRelay process exited with code ${code}`)
            });
        }
    }
    
    private startProcess(name: 'dbot' | 'vrelay') {
        if (name === 'dbot') {
            this.discordBotStatus = ProcessStatus.Starting
            const args = [this.dcommander.pipesPrefix, process.env.DISCORD_LOGIN_TOKEN]
            if (this.dcommander.guildId) {
                args.push(this.dcommander.guildId)
                args.push(this.dcommander.channelId)
            }
            this.dbot = this.spawnExecutable(process.env.DBOT_PATH, args)

        } else if (name === 'vrelay') {
            this.vivoxRelayStatus = ProcessStatus.Starting
            const args = [this.vcommander.pipesPrefix]
            if (this.vcommander.guildId) {
                args.push(this.vcommander.guildId)
                args.push(this.vcommander.channelId)
            }
            shell.ls('/discordbot/bin')

            this.vrelay = this.spawnExecutable(process.env.VRELAY_PATH, args)
        }
    }
    private spawnExecutable(executablePath: string, args: string[]) {
        if (process.env.DONT_SPAWN) return;
        l.info(`spawning process ${executablePath}`)
        return child_process.spawn(executablePath, args)
    }

    /**
     * @method ensureHealth Pings vivox relay and discord bot
     * @returns [true, true] if all pings succeed
     */
    private keepAliveLoop() {
        return Promise.all([
            this.checkVivoxRelay(),
            this.checkDiscordBot()
        ]).then(result => {

            if (!result[0]) {
                this.vivoxRelayStatus = lowerStatus(this.vivoxRelayStatus, ProcessStatus.Failing)
                l.warn(`vivox relay failed to respond -> ${this.vivoxRelayStatus.toString()}`)
                if (this.vivoxRelayStatus < ProcessStatus.Starting) {
                    if (this.vrelay) {
                        this.vivoxRelayStatus = ProcessStatus.Stopping
                        this.vrelay.kill()
                    }
                    this.spawnProcess('vrelay')
                }
                
            } else {
                this.vivoxRelayStatus = raiseStatus(this.vivoxRelayStatus, ProcessStatus.Active)
            }

            if (!result[1]) {
                this.discordBotStatus = lowerStatus(this.discordBotStatus, ProcessStatus.Failing)
                l.warn(`discord bot failed to respond -> ${this.discordBotStatus.toString()}`)
                if (this.discordBotStatus < ProcessStatus.Starting) {
                    if (this.dbot) {
                        this.discordBotStatus = ProcessStatus.Stopping
                        this.dbot.kill()
                    }
                    this.spawnProcess('dbot')
                }

            } else {
                this.discordBotStatus = raiseStatus(this.discordBotStatus, ProcessStatus.Active)
            }

            // repeat
            if (!this.stopping) {
                this.nextCheckToken = setTimeout(() => this.keepAliveLoop(), 8000)
            }
        })
    }
    
    async checkDiscordBot() {
        return this.dcommander.ping()
    }

    async checkVivoxRelay() {
        return this.vcommander.ping()
    }

    dbotLog(data) {
        l.dbot(intArrayToString(data))
    }
    
    vrelayLog(arr) {
        let str = '';

        for (let i = 0; i < arr.length; i++) {
            str += '%' + ('0' + arr[i].toString(16)).slice(-2);
        }
        str = decodeURIComponent(str).trim();
        // ignore this useless vivox warning
        if (str.match('rtp_parse: timestamp jump from')) return
        const match = str.match(/\[(\w+)\] /)
        if (match == null) {
            l.vrelay(str)
        } else {
            str = 'VRELAY | ' + str.slice(match.index + match[0].length)
            switch (match[1]) {
                case 'Trace':
                    l.trace(str)
                    break
                case 'Debug':
                    l.debug(str)
                    break
                case 'Info':
                    l.info(str)
                    break
                case 'Warn':
                    l.warn(str)
                    break
                case 'Error':
                    l.error(str)
                    break
                case 'Fatal':
                    l.fatal(str)
                    break
                
            }
        }
    }
}

enum ProcessStatus {
    Active = 5, // alive and succeeded last ping check
    Starting = 4, // start process call has begin
    Failing = 2, // didnt respond to last ping
    Stopping = 1, // being killed
    Stopped = 0, // not running
}

function lowerStatus(from: ProcessStatus, to: ProcessStatus): ProcessStatus {
    if (to < from) return to
    return from
}

function raiseStatus(from: ProcessStatus, to: ProcessStatus): ProcessStatus {
    if (to > from) return to
    return from
}