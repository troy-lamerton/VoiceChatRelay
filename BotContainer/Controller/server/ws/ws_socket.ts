import l from '../common/logger'
import { eventPromise, eventPromiseFiltered } from '../common/promise_utils';
import server from './ws_server'

/**
 * Provides methods to send and receive messages to a websocket client
export default abstract class WebSocketSingle {
    prefix: string

    protected connection?: WebSocket

    protected forwardMessage: (msg: Message) => void

    constructor(prefix: string, forwardMessage: (msg: Message) => void) {
        this.prefix = prefix
        this.forwardMessage = forwardMessage
        l.info('ws on', server.address())
        server.on('connection', (ws, sock) => {
            l.info('connection', ws, sock)
        })
        this.init()
    }

    private init() {
        this.socket.on('connection', conn => {
            l.info('Someone connected to ' + this.prefix)
            this.connection = conn
            conn.on('close', () => {
                this.connection = undefined
            })
            conn.on('data', data => {
                try {
                    const message = new Message(data)
                    this.socket.emit('message', message)
                } catch (err) {
                    l.error(err)
                }
            })
        })
        this.socket.on('message', (message: Message) => {
            this.handleMessage(message)
            this.forwardMessage(message)
        })
    }

    abstract handleMessage(message: Message)

    async send(msg: Message) {
        if (!this.connection) return false

        return new Promise(resolve => {

            this.connection.write(msg.string(), err => {
                if (err) l.error(err)
                resolve(!err)
            })
        })
    }
    
    async sendForResult(msg: Message, replyCommand: string): Promise<Message> {
        if (!this.connection) return Promise.resolve(null)
        if (!this.send(msg)) {
            throw new Error(`Sending ${msg.command} message failed`)
        }
        return eventPromiseFiltered(this.connection!, 'message', (msg: Message) => msg.command === replyCommand, 2000)
    }

    async ping() {
        try {
            const res = await this.sendForResult(Message.Ping, 'pong')
            return !!res
        } catch {
            return false
        }
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

    string() {
        return `${this.command};${this.contents}`
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
*/