import l from '../common/logger'

import PipeClient, { Message } from './pipe_client'
import { eventPromise } from '../common/promise_utils';

export default class ControlPipe extends PipeClient {
    public guildId: string
    public channelId: string

    join(guildId: string, channelId: string) {
        this.guildId = guildId
        this.channelId = channelId
        return this.send(Message.Join(guildId, channelId))
    }

    async leaveChannel() {
        const result = eventPromise(this.emitter, 'left', 'error')
        if (!this.send(Message.Leave)) return false
        return await result
    }

    handleMessage(message: Message) {
        if (message.command === 'left') {
            this.guildId = '-1'
            this.channelId = '-1'
        }
    }
}
