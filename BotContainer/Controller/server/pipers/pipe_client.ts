import l from '../common/logger'
import { EventEmitter } from 'events';
import { WritablePipe, ReadablePipe } from './named_pipe'
import { eventPromise, eventPromiseFiltered } from '../common/promise_utils';

/**
 * Provides methods to send and receive messages on this machine
 * Uses two named pipes to communicate
 */
export default abstract class PipeClient {
    private pipePrefix: string
    
    /** an outgoing pipe that client is reading from */
    protected writableServer: WritablePipe
    protected readableServer: ReadablePipe

    /** emits events from the readable pipe */
    protected emitter: EventEmitter

    protected forwardMessage: (msg: Message) => void

    constructor(pipePrefix: string, forwardMessage: (msg: Message) => void) {
        this.pipePrefix = pipePrefix
        this.forwardMessage = forwardMessage
        this.init()
    }

    get pipesPrefix(): string {
        return this.pipePrefix
    }

    private init() {
        this.emitter = new EventEmitter()

        this.emitter.on('message', (message: Message) => {
            this.forwardMessage(message)
            this.handleMessage(message)
        })

        this.writableServer = new WritablePipe(this.pipePrefix, this.emitter)
        this.readableServer = new ReadablePipe(this.pipePrefix, this.emitter)
    }

    abstract handleMessage(message: Message)

    send(msg: Message): boolean {
        return this.writableServer.send(msg)
    }

    async sendForResult(msg: Message, replyCommand: string): Promise<Message> {
        if (!this.send(msg)) {
            throw new Error(`Sending ${msg.command} message failed`)
        }
        return eventPromiseFiltered(this.emitter, 'message', (msg: Message) => msg.command === replyCommand, 2000)
    }

    async ping() {
        return this.writableServer.ping()
    }
}

export class Message {
    command: string
    contents: string

    constructor(data: string) {
        let parts = data.split(';')
        this.command = parts[0].trim()
        this.contents = parts.length > 1 ? parts[1].trim() : ''
    }

    static Create(command: string, contents: string = ''): Message {
        return new Message(`${command};${contents}`)
    }

    static get Ping() {
        return Message.Create('ping')
    }

    static get Pong() {
        return Message.Create('pong')
    }

    static get Info() {
        return Message.Create('info')
    }
    
    static get Leave() {
        return Message.Create('leave')
    }

    static Join(guild: string, channel: string) {
        return Message.Create('join', `${guild},${channel}`)
    }
}
