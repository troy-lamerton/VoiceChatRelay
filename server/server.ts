import Fastify from 'fastify'
import agent from 'superagent'
import l from './common/logger';
import ContainerPool, {getRunningContainersPool} from './bots/bot_pool'
import { ChannelsTable, Channel } from './dynamo/data';
import { ServerResponse } from 'http';

const DEBUG = !!process.env.DEBUG
const randomName = DEBUG && require('node-random-name')

/**
 * @param port Port to listen on
 */
export default async function runManagerServer(port: number) {
    const containerPool = await getRunningContainersPool() //new ContainerPool(process.env.BOT_CONTAINER_NAME)
    
    const fastify = Fastify({ logger: { 
        level: 'info',
        prettyPrint: {
            translateTime: 'HH:MM:ss.L',
            levelFirst: false,
            ignore: 'pid,hostname',
        },
    }})

    fastify.get('/', (req, reply) => {
        reply.code(200).send('I am the manager of all bot containers')
    })
    fastify.get('/health', (req, reply) => {
        reply.code(200).send(`${containerPool.idleCount} of ${containerPool.count} running bot containers are healthy`)
    })

    fastify.get('/info', (req, reply) => {
        reply.code(200).send(containerPool.containerStatuses)
    })

    /**
     * Gets the channel from dynamo
     * Gets bot running on this channel
     * If one does not exist, spawns a new bot container and joins the channel
     */
    fastify.get('/channel_joined/:id', async (req, reply) => {
        const { id } = req.params
        const channels = new ChannelsTable()
        if (DEBUG) {
            await channels.createChannelIfNotExists(id, Math.round(100000 + Math.random() * 90000000).toString())
            await channels.addUser(id, randomName())
        }
        const channel = await channels.getChannel(id)
        if (!channel) {
            reply.code(404).send(`channel with id ${id} is not in dynamo db!`)
            return
        }
        
        const existingBot = containerPool.getBotInChannel(channel.id)
        if (existingBot) {
            reply.code(200).send({
                text: `A running container is already in that channel`,
                container: existingBot
            })
            return
        }

        await containerJoinChannel(containerPool, channel, reply)
    })

    /**
     * Gets the channel from dynamo
     * If there are no users left in the channel,
     * Gets the bot running on this channel and tells it to leave
     */
    fastify.get('/channel_left/:id', async (req, reply) => {
        const { id } = req.params
        const channels = new ChannelsTable()
        const channel = await channels.getChannel(id)
        if (!channel) {
            reply.code(404).send({
                error: true,
                error_in: 'your fault',
                text: `channel with id ${id} is not in dynamo db!`
            })
            return
        }
        
        const container = containerPool.getBotInChannel(channel.id)

        if (!container) {
            reply.code(200).send({
                text: `There is no container running for channel ${channel.id}, ignored`
            })
            return
        }

        try {
            const res = await agent.post(`${container.controllerAddress}/leave`)
            reply.code(200).send({
                text: 'OK'
            })
        } catch (err) {
            reply.code(501).send({
                error: true,
                text: 'The container responded with an error to /leave',
                error_in: `container:${container.controllerAddress}`,
                container,
                containerResponse: err
            })
        }
    })

    // so what happens is the cos player requests game server 'i want to join sodapopin/general voice channel'
    // game server adds this to DYNAMO and send an SNS push to...

    // VOICE RELAY MANAGER
    // reads the dynamo row
    // create vivox join channel token for relaybot
    // gets an idle bot container from the pool and http post to the bot controller
        // POST /join
        // vivox_token: r434.adsf4f3434.asdfst30fka
        // channel: general
    // [async - starts a new container if needed]


    fastify.get('/join/:guild/:channel', async (req, reply) => {
        /** @param guild the discord guild id */
        /** @param channel the discord channel id */
        try {
            const { guild, channel } = req.params
            const botController = await containerPool.getIdleOrRunContainer()
            if (!botController) {
                reply.code(500).send({
                    error: true,
                    error_in: 'relay manager',
                    text: 'Could not get an idle bot container or run a new one'
                })
            }
            botController.idle = false
            try {
                await agent
                    .post(`${botController.controllerAddress}/join`)
                    .send({
                        guild,
                        channel,
                    })
                reply.code(200).send({
                    channel,
                    error: false,
                })
                
            } catch (err) {
                botController.idle = true

                l.error(err)
                if (!err.response) {
                    reply.code(500).send({
                        error: true,
                        error_in: `controller:${botController.controllerPort}`,
                        text: err.msg || err
                    })
                    return
                }
                reply.code(500).send({
                    error: true,
                    status: err.response.status,
                    error_in: `controller:${botController.controllerPort}`,
                    text: JSON.parse(err.response.text),
                })
            }
            
        } catch (err) {
            l.error(err)
            reply.code(500).send({
                error: true,
                error_in: 'relay manager',
                text: err,
            })
        }
    })

    try {
        await fastify.listen(port)
        // await botPool.killAllBots()
        // await botPool.runBotContainers(2)
    } catch (err) {
        l.error(err)
        process.exit(1)
    }
}


async function containerJoinChannel(containerPool: ContainerPool, channelToJoin: Channel, reply: Fastify.FastifyReply<ServerResponse>) {
    const { guild, id: channel } = channelToJoin
    
    try {
        const container = await containerPool.getIdleOrRunContainer()
        
        const result = {
            error: false,
            channel,
            container,
        }
        
        if (!container) {
            reply.code(500).send({
                ...result,
                error: true,
                error_in: 'relay manager',
                text: 'Could not get an idle bot container or run a new one'
            })
            return
        }

        container.idle = false

        try {
            await agent
                .post(`${container.controllerAddress}/join`)
                .send({
                    guild,
                    channel,
                })
            
            container.channel = channelToJoin.id

            reply.code(200).send(result)
            
        } catch (err) {
            container.idle = true
            
            reply.code(500).send({
                ...result,
                error: true,
                error_in: `controller:${container.controllerPort}`,
                text: err.response ? JSON.parse(err.response.text) : (err.msg || err)
            })
        }
        
    } catch (err) {
        reply.code(500).send({
            error_in: 'container manager',
            text: err,
        })
    }
}