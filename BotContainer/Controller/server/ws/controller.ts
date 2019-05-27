import l from '../common/logger'

import { eventPromise } from '../common/promise_utils';

export default class Controller {
    public guildId: string
    public channelId: string

    constructor(a: any, b: any) {

    }

    join(guildId: string, channelId: string) {
        this.guildId = guildId
        this.channelId = channelId
        return true
        // return this.send(Message.Join(guildId, channelId))
    }

    async leaveChannel() {
        // const result = eventPromise(this.connection, 'left', 'error')
        // if (!this.send(Message.Leave)) return false
        // return await result
        return true
    }

    handleMessage(message: Message) {
        if (message.command === 'left') {
            this.guildId = '-1'
            this.channelId = '-1'
        }
    }

    send(msg: any) {

    }
}
