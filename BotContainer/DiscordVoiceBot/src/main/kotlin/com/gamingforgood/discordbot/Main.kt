package com.gamingforgood.discordbot

import net.dv8tion.jda.core.JDA
import net.dv8tion.jda.core.JDABuilder
import net.dv8tion.jda.core.audio.AudioReceiveHandler
import net.dv8tion.jda.core.audio.AudioSendHandler
import net.dv8tion.jda.core.audio.CombinedAudio
import net.dv8tion.jda.core.audio.UserAudio
import net.dv8tion.jda.core.entities.Member
import net.dv8tion.jda.core.entities.Message
import net.dv8tion.jda.core.entities.User
import net.dv8tion.jda.core.events.ReadyEvent
import net.dv8tion.jda.core.events.channel.text.TextChannelDeleteEvent
import net.dv8tion.jda.core.events.guild.voice.GuildVoiceMoveEvent
import net.dv8tion.jda.core.events.message.MessageDeleteEvent
import net.dv8tion.jda.core.events.message.MessageReceivedEvent
import net.dv8tion.jda.core.hooks.ListenerAdapter
import java.nio.ByteBuffer
import java.nio.ByteOrder

lateinit var discord: JDA

val server = UdpServer(9050)

lateinit var botController: ControllerClient

val DEBUG = System.getenv().containsKey("DEBUG")
val CONTROLLER_PORT = System.getenv()["PORT_CONTROLLER_WS"]

fun main(args: Array<String>) {
    check(!CONTROLLER_PORT.isNullOrEmpty()){"PORT_CONTROLLER_WS env missing - specify the ws port of the controller server"}
    log("main", "DBot started with ${args.size} args: ${args.joinToString(", ")}")

    if (args.size < 2) {
        throw IllegalArgumentException("Not enough program args! You must specify: pipesPrefix discordLoginToken")
    }

    val pipesPrefix = args[0]
    val autoJoinArgs = if (args.size >= 4) arrayOf(args[2], args[3]) else arrayOf()

    // blocking
    val wsPort = CONTROLLER_PORT.toInt()
    botController = ControllerClient(wsPort, pipesPrefix)

    discord = JDABuilder(args[1])
        .addEventListener(MainDiscordListener(botController, autoJoinArgs))
        .addEventListener()
        .build()!!

//    debugPipes(pipesPrefix)

    // start audio relay server socket
    server.start()
}

interface IDiscordListener {
    fun onDiscordMessageReceived(message: Message)
    fun onDiscordMessageDeleted(id: Long)
    fun onGuildVoiceMove(event: GuildVoiceMoveEvent)
}

class MainDiscordListener(private val extraListener: IDiscordListener, private val autoJoinArgs: Array<String>) : ListenerAdapter() {
    override fun onMessageReceived(event: MessageReceivedEvent) {
        val message = event.message
        if (message.contentRaw == "/ping") {
            val channel = event.channel
            channel.sendMessage("Pong!").queue()
        } else {
            extraListener.onDiscordMessageReceived(message)
        }
    }

    override fun onMessageDelete(event: MessageDeleteEvent) {
        extraListener.onDiscordMessageDeleted(event.messageIdLong)
    }

    // TODO: move this into the text chat scraper discord bot
    override fun onTextChannelDelete(event: TextChannelDeleteEvent) {
        if (event.channel.name == Config.DiscordCosModerationChannel) {
            // recreate cos moderation channel when deleted
            log("discord", "channel deleted ${event.channel.name}, recreating")
            event.guild.controller.createTextChannel(event.channel.name)
                .setParent(event.channel.parent)
                .queue()
        }
    }

    override fun onGuildVoiceMove(event: GuildVoiceMoveEvent) {
        extraListener.onGuildVoiceMove(event)
    }

    override fun onReady(event: ReadyEvent) {
        if (autoJoinArgs.isNotEmpty()) {
            botController.handleMessage(Message("join", autoJoinArgs.joinToString(",")))
            return
        }
        if (DEBUG) {
            botController.handleMessage(Message("join", "551446223496675329,551446224000253973"))
        }
    }

}

class ReceiveDiscordAudio : AudioReceiveHandler {

    private val monoBytes = ByteArray(1920) // reused every 20ms
    private val monoBuffer = ByteBuffer.wrap(monoBytes) // reused every 20ms

    init {
        // mono buffer in little endian
        // the resampler in C# needs little endian (because windows system is little endian)
        monoBuffer.order(ByteOrder.LITTLE_ENDIAN)
    }

    private val interleave = false

    /**
     * Mix discords stereo big endian audio to mono channel little endian
     * And send it to the vrelay udp client
     */
    override fun handleCombinedAudio(combinedAudio: CombinedAudio) {
        val start = System.currentTimeMillis()

        /** raw stereo data from discord, see [AudioReceiveHandler.OUTPUT_FORMAT] */
        val discordData = combinedAudio.getAudioData(1.0)

        val stereoBuffer = ByteBuffer.wrap(discordData)

        // mix each frame to mono
        for (i in 0 until discordData.size step 4) {
            // combine stereo channels into mono
            val monoFrame: Short = if (interleave) {
                // take left then take right, alternating
                if (i / 4 % 2 == 0) {
                    stereoBuffer.getShort(i)
                } else {
                    stereoBuffer.getShort(i + 2)
                }
            } else {
                // mix left and right together - this sounds a bit better
                val average = (stereoBuffer.getShort(i) + stereoBuffer.getShort(i + 2)) / 2
                average.toShort()
            }

            monoBuffer.putShort(i / 2, monoFrame)
        }

        // send to udp client
        server.sendToClient(monoBytes)

        val elapsed = System.currentTimeMillis() - start
        if (DEBUG) {
            require(elapsed < 20) { "Took $elapsed ms! (>= 20) The next call will have messed with the reused buffer" }
        }
        assert(elapsed <= 5) { "Mixing discord stereo to mono and sending took $elapsed ms!" }
    }

    override fun canReceiveCombined(): Boolean {
        return true
    }

    override fun canReceiveUser(): Boolean { return false }
    override fun handleUserAudio(userAudio: UserAudio?) { }
}

class SendRoomAudio : AudioSendHandler {
    private val buffer: ToDiscordBuffer = ToDiscordBuffer.bufferObject

    override fun provide20MsAudio(): ByteArray {
        return buffer.read20MsOfAudio()
    }

    override fun canProvide(): Boolean {
        return buffer.isDataReady
    }

    override fun isOpus(): Boolean {
        return false
    }
}


// helper extensions

fun Member.isSelf(): Boolean {
    return discord.selfUser.idLong == this.user.idLong
}
fun User.isSelf(): Boolean {
    return discord.selfUser.idLong == this.idLong
}