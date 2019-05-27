import fastify from 'fastify'
import l from './common/logger'

import Controller from './ws/controller';
import AliveProcessManager from './manager/process_manager'
import { Message } from './ws/ws_socket';

const vcommander = new Controller(process.env.VRELAY_PIPE_PREFIX, forwardMessage('discord'));
const dcommander = new Controller(process.env.DBOT_PIPE_PREFIX, forwardMessage('vivox'));
// debug channel
dcommander.guildId = vcommander.guildId = '551446223496675329'
dcommander.channelId = vcommander.channelId = '551446224000253973'

const shouldForward = (command: string) => command === 'speaking'

function forwardMessage(to: 'discord' | 'vivox') {
    if (to === 'discord') {
        return (message: Message) => {
            if (shouldForward(message.command)) dcommander.send(message)
        }
    } else {
        return (message: Message) => {
            if (shouldForward(message.command)) vcommander.send(message)
        }
    }
}

async function health (_, reply) {
    const dbot = await dcommander.ping()
    const vrelay = await vcommander.ping()
    reply.send({
        dbot,
        vrelay
    })
}

export default function runServer(port: number) {
    const server = fastify({logger: l})
    server.get('/', (_, reply) => {
        reply.send('I am a bot controller')
    })

    server.get('/health', health)

    server.post('/join', (req, reply) => {
        l.info(`Join request: ${req.body}`)
        const { guild, channel } = req.body
        
        const resultV = vcommander.join(guild, channel)
        const resultD = dcommander.join(guild, channel)
        
        const OK = resultV && resultD
        
        reply.code(OK ? 200 : 501).send({
            told_dbot: resultD,
            told_vrelay: resultV,
        })
    })

    server.post('/leave', async (_, reply) => {
        l.info('Leave request')
        
        const resultV = await vcommander.leaveChannel()
        const resultD = await dcommander.leaveChannel()
        
        const OK = resultV && resultD
        
        reply.code(OK ? 200 : 501).send({
            dbot_left: resultD,
            vrelay_left: resultV,
        })
    })

    server.get('/info', async (req, reply) => {
        try {
            const dbotInfo = await dcommander.sendForResult(Message.Info, 'info')
            const vrelayInfo = await vcommander.sendForResult(Message.Info, 'info')
            
            const OK = dbotInfo && vrelayInfo && (dbotInfo.contents === vrelayInfo.contents)
            const parts = dbotInfo.contents.split(',')
            
            const result = {
                error: !OK,
                guild: parts[0],
                channel: parts[1],
                dbot: dbotInfo.contents,
                vrelay: vrelayInfo.contents,
            }

            reply.code(OK ? 200 : 501)

            if (OK) {
                reply.send(result)
            } else {
                reply.send({
                    ...result,
                    text: 'dbot and vrelay are not in the same channel'
                })
            }

        } catch (err) {
            reply.code(500).send({
                error: true,
                text: err.msg || err
            })
        }
    })
    
    const keepAlive = new AliveProcessManager(dcommander, vcommander)
    keepAlive.start()

    server.get('/kill', () => {
        keepAlive.killall()
    })
    // 0.0.0.0 listens on all ips, required to work in docker container
    server.listen(port, '0.0.0.0', err => {
        if (err) throw err
        l.info(`Controller listening on ${(server.server.address() as any).port}`)
    })
}
