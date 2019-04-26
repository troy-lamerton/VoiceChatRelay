import l from '../common/logger'
import net, { Server, Socket } from 'net'
import { eventPromise } from '../common/promise_utils';
import { EventEmitter } from 'events';
import { Message } from './pipe_client'

abstract class NamedPipe {
    abstract name: string

    protected readonly pipePrefix: string
    protected readonly commonEmitter: EventEmitter
    protected server: Server
    protected activeStream: Socket

    constructor(pipePrefix: string, emitter: EventEmitter) {
        this.pipePrefix = pipePrefix
        this.commonEmitter = emitter
        this.createServer()
    }

    protected abstract configureStream(stream: Socket)

    private createServer() {
        this.server = net.createServer((stream) => {
            this.activeStream = stream
            // called when client starts writing to the pipe

            l.info(`Client connected to ${this.name}`)
            
            // emit error to listeners
            stream.on('error', data => {
                this.commonEmitter.emit('error', data)
            })
            stream.on('timeout', data => {
                this.commonEmitter.emit('timeout', data)
            })

            this.configureStream(stream)
        })

        this.server.on('close', () => {
            l.warn(`Server for ${this.name} closed`)
        })
        this.server.listen(this.getPipePath(), () => {
            l.debug(`Waiting for client on ${this.name}`)
        })
    }

    getPipePath() {
        return `\\\\.\\pipe\\${this.name}`
    }
}

export class WritablePipe extends NamedPipe {
    configureStream(_: Socket) {
        this.commonEmitter.on('ping', () => {
            this.pong()
        })
    }

    get name(): string {
        return this.pipePrefix + '_server'
    }

    public async ping(timeout: number = 1500): Promise<boolean> {
        if (!this.canSend()) return false

        let result = eventPromise(this.commonEmitter, 'pong', 'error', timeout)
        if (!this.send(Message.Ping)) return false
        return result
    }

    private canSend(): boolean {
        if (!this.activeStream) {
            l.warn(`Cannot send on ${this.pipePrefix}. No client.`)
            return false
        }
        return true
    }
    
    public pong(): boolean {
        return this.send(Message.Pong)
    }
    
    send(message: Message): boolean {
        // l.debug(`[${this.name}]: [${message.command}] ${message.contents}`)
        return this.write(`${message.command};${message.contents}`)
    }
    
    protected write(data: string): boolean {
        if (!this.canSend()) return false

        try {
            this.activeStream.write(data + '\n')
            return true
        } catch(err) {
            l.error(err)
        }
        return false
    }
}

export class ReadablePipe extends NamedPipe {
    get name() {
        return this.pipePrefix + '_client'
    }

    configureStream(stream: Socket) {
        stream.on('data', data => {
            let message = new Message(data.toString())
            this.commonEmitter.emit('message', message)
            this.handleCommonCommands(message)
            data.toString()
        })
    }

    private handleCommonCommands(message: Message) {
        switch (message.command) {
            case 'pong':
                this.commonEmitter.emit('pong')
                break
                
            case 'error':
                l.error(`Error from ${this.name}: ${message.contents}`)
                break
                
            default:
                // l.debug(`[${this.name}]: [${message.command}] [${message.contents}]`)
                break
        }
    }
}