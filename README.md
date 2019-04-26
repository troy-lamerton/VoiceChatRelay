# Voice chat for Clash of Streamers

## Why

People in our game want to speak with their buddies in Discord.

To do this, we need to manage connections to all voice channels that players are speaking/listening in.

Therefore, we need an easy way to run and control every [Bot Container](../BotContainer/README.md). One Bot Container per Discord voice channel.

## How

You tell the manager to activate a Discord voice channel for our players. Note that one of our Discord bots must already be in the target Discord server. Managing the different Discord bot accounts is not (yet!) implemented.

### Debugging

- Send a GET request to activate a Discord voice channel by id
- Send a GET request to get the status of all Bot Containers

### Production (TODO)

Polls DynamoDB for table of channels. Row contains information like who is in the channel. If a CoS player is in the channel then a [Bot Container](../BotContainer/README.md) should be connected to the channel.

### Production v2 (TODO later)

Listens for Amazon SNS messages which tell it which row changed in the table. Then it won't need to poll Dynamo because it is notified when a row changes.