import AWS, { DynamoDB } from 'aws-sdk'
import { KeySchema, AttributeDefinitions, PutItemInputAttributeMap, AttributeValue } from 'aws-sdk/clients/dynamodb';

export const dynamodb = new AWS.DynamoDB();
export const client = new AWS.DynamoDB.DocumentClient()

export default class SimpleDynamo {

    public static CreateTable(TableName: string, KeySchema: KeySchema, AttributeDefinitions: AttributeDefinitions, throughputUnits = 10) {
        return dynamodb.createTable({
            TableName,
            KeySchema,
            AttributeDefinitions,
            ProvisionedThroughput: {
                ReadCapacityUnits: throughputUnits,
                WriteCapacityUnits: throughputUnits
            }
        }).promise()
        .then(data => {
            console.log('Created table:', data.TableDescription.TableName, `Status: ${data.TableDescription.TableStatus}`)
        })
    }

    public static DeleteChannelsTable() {
        return dynamodb.deleteTable({
            TableName: 'Channels'
        }).promise()
    }

    public static CreateChannelsTable() {
        const KeySchema = [
            { AttributeName: 'id', KeyType: 'HASH'}, // Partition key
        ]
        const AttributeDefinitions = [
            { AttributeName: 'id', AttributeType: 'S' },
        ]

        return SimpleDynamo.CreateTable('Channels', KeySchema, AttributeDefinitions).then(_ => {
            return new ChannelsTable()
        })
    }
    
}

class Table {
    tableName: string

    constructor(name: string) {
        this.tableName = name
    }

    putItem(Item: {[key: string]: any}) {
        return client.put({
            TableName: this.tableName,
            Item
        }).promise()
    }
}


export type Channel = {
    id: string,
    guild: string,
    usersInChannel: Set<string>
}

export class ChannelsTable extends Table {
    constructor() {
        super('Channels')
    }

    put(id: string, guildId: string, usersInChannel: string[] = undefined) {
        return super.putItem({
            id,
            guild: guildId,
            usersInChannel: usersInChannel && client.createSet(usersInChannel)
        })
    }

    async getChannel(id: string): Promise<null | Channel> {
        const channel = await client.get({
            TableName: this.tableName,
            Key: { id },
        }).promise()
        if (!channel.Item) return null

        return {
            id,
            guild: channel.Item.guild,
            usersInChannel: new Set(channel.Item.usersInChannel ? channel.Item.usersInChannel.values : [])
        }
    }

    async createChannelIfNotExists(id: string, guild: string) {
        const existing = await this.getChannel(id)
        if (!existing) return await this.put(id, guild)
        return existing
    }

    async getUsersInChannel(id: string): Promise<string[]> {
        return client.get({
            TableName: this.tableName,
            Key: { id },
        }).promise().then(res => res.Item ? res.Item.usersInChannel.values : [])
    }

    async addUser(id: string, newUser: string) {
        const res = await client.update({
            TableName: this.tableName,
            Key: { id },
            UpdateExpression: "ADD usersInChannel :extra",
            ExpressionAttributeValues: {
                ':extra': client.createSet([newUser]),
            },
            ReturnValues: "UPDATED_NEW"
        }).promise();
        console.log(res);
        return res
    }

    async removeUser(id: string, goneUser: string) {
        const res = await client.update({
            TableName: this.tableName,
            Key: { id },
            UpdateExpression: "DELETE usersInChannel :gone",
            ExpressionAttributeValues: {
                ':gone': client.createSet([goneUser]),
            },
            ReturnValues: "UPDATED_NEW"
        }).promise();
        console.log(res);
        return res
    }
}