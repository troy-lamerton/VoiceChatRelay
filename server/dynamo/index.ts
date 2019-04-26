import '../common/env';
import './configure'

import SimpleDynamo, { ChannelsTable } from './data';
import l from '../common/logger'

(async () => {

    const channels = new ChannelsTable()

    await SimpleDynamo.DeleteChannelsTable()
    await SimpleDynamo.CreateChannelsTable()

    await channels.put('259754', '575')

    // await channels.addUser('259754', 'Jim')
    // await channels.addUser('259754', 'Derry')
    // await channels.removeUser('259754', 'Jim')
    // const users = await channels.getUsersInChannel('259754')
    // l.info(users)

})()