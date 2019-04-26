package com.gamingforgood.discordbot

import net.dv8tion.jda.core.EmbedBuilder
import net.dv8tion.jda.core.audio.hooks.ConnectionListener
import net.dv8tion.jda.core.audio.hooks.ConnectionStatus
import net.dv8tion.jda.core.entities.Guild
import net.dv8tion.jda.core.entities.MessageEmbed
import net.dv8tion.jda.core.entities.User
import net.dv8tion.jda.core.entities.VoiceChannel
import net.dv8tion.jda.core.events.guild.voice.GuildVoiceMoveEvent
import java.lang.Exception
import kotlin.collections.HashSet

class ControlPipe(pipesPrefix: String) : PipeClient(pipesPrefix), IDiscordListener, ConnectionListener {

    private var targetGuild: Guild? = null
    private var currentChannel: VoiceChannel? = null
    private var playersSpeakingMessageId: Long? = null
    private var playersInChannel = HashSet<String>() // the players in the vivox channel

    /**
     * Handle commands from the controller
     */
    override fun handleMessage(message: Message) {
        when (message.command) {

            "join" -> {
                val parts = message.contents.split(",")
                val guildId = parts[0]
                val channelId = parts[1]
                log("bot_command", "join voice channel $guildId / $channelId")

                val targetGuild = discord.guilds.find { guild -> guild.id == guildId}
                val voiceChannel = targetGuild!!.getVoiceChannelById(channelId)
                val mgr = targetGuild.audioManager

                // start accepting audio data from the game when discord audio connects
                mgr.connectionListener = this

                mgr.sendingHandler = SendRoomAudio() // must be set to enable receiving audio
                mgr.setReceivingHandler(ReceiveDiscordAudio())

                // join voice channel
                mgr.openAudioConnection(voiceChannel)

                this.targetGuild = targetGuild
                this.currentChannel = voiceChannel

                // get players speaking message from a previous run
                val cosModerationChannel = targetGuild.getTextChannelsByName(Config.DiscordCosModerationChannel, false)?.first()
                if (cosModerationChannel != null) {
                    playersSpeakingMessageId = cosModerationChannel.iterableHistory
                        .limit(5)
                        .submit()
                        .get()
                        .find {msg -> msg.author.isSelf()}?.idLong
                }

                // still connecting to discord audio, but here is good enough
                send(Message.joined(guildId, channelId))
            }

            "leave" -> {
                leaveChannel()
            }

            "speaking" -> {
                if (targetGuild == null) {
                    log("pipe", "got speaking command but not connected to voice channel")
                    return
                }

                val modChannel = targetGuild!!.getTextChannelsByName(Config.DiscordCosModerationChannel, false)?.first()

                when {
                    modChannel == null -> {
                        log("pipe", "modChannel == null")
                        // wait for Main.kt to finish creating the channel
                        // skipping this speaking update is fine
                        return
                    }

                    !modChannel.canTalk() -> {
                        log("pipe", "modChannel cant talk")
                        // someone is messing with the permissions
                        leaveChannel()
                        return
                    }

                    else -> {

                        // TODO: handle the speaking message being longer than 2048 embed desc char limit
                        //  needs to be done to support > 50 cos players
                        //  by truncating the cos player names and maybe removing the commands on the silent ones
                        //  actually just merge the three links into one

                        val playersSpeaking = message.contents.split(",")
                        playersSpeaking.forEach {name -> if (name.isNotEmpty()) playersInChannel.add(name)}

//                        log("pipe", "in channel: $playersInChannel, speaking: $playersSpeaking")

                        val msg = getSpeakingMessage(playersSpeaking)

                        if (playersSpeakingMessageId != null) {
                            modChannel.editMessageById(playersSpeakingMessageId!!, msg).queue()
                        } else {
                            log("pipe", "create new speaking msg in #${modChannel.name}")
                            try {
                                modChannel.sendMessage(msg).queue()
                            } catch (ex: Exception) {
                                playersSpeakingMessageId = null

                                log("discord", "${ex.javaClass.name}: ${ex.message}")
                            }
                        }
                    }
                }

            }

            "info" -> {
                val guildId = targetGuild?.id
                val channelId = currentChannel?.id
                send(Message.info(guildId, channelId))
            }

        }
    }

    private fun leaveChannel() {
        targetGuild?.audioManager?.closeAudioConnection()
        targetGuild = null
        currentChannel = null
        botController.send(Message.left)
    }

    // update discord message showing players speaking
    private fun sendSpeaking(names: List<String>?) {
        send(Message.speaking(names))
    }

    /**
     * Create formatted discord message
     */
    private fun getSpeakingMessage(speakingPlayers: List<String>): MessageEmbed {
        val description = playersInChannel.joinToString("\n") { playerName ->
            val speakingIcon = if (speakingPlayers.contains(playerName)) "💬" else "🕳️"
            val muteUrl = "http://google.com/search?q=mute"
            val kickUrl = "http://google.com/search?q=kick"
            val banUrl = "http://google.com/search?q=ban"
            "`$speakingIcon $playerName` [:mute:]($muteUrl) | [:boot:]($kickUrl) | [:hammer:]($banUrl)"
        }.plus("\n\nType /help for mod commands")

        return EmbedBuilder().setColor(3447003) // blue
            .setTitle("Speaking in ${currentChannel?.name}")
            .setDescription(description)
            .build()
    }

    override fun onDiscordMessageReceived(message: net.dv8tion.jda.core.entities.Message) {
        if (message.author.isSelf()) {
            // the message is my players speaking message
            playersSpeakingMessageId = message.idLong
        }
    }

    override fun onDiscordMessageDeleted(id: Long) {
        if (id == playersSpeakingMessageId) playersSpeakingMessageId = null
    }

    /**
     * When moved from the target voice channel, rejoin it
     */
    override fun onGuildVoiceMove(event: GuildVoiceMoveEvent) {
        if (event.member.isSelf()) {
            if (targetGuild == null) return
            if (event.channelLeft.idLong == currentChannel?.idLong) {
                // rejoin voice channel
                targetGuild!!.audioManager.openAudioConnection(currentChannel)
            }
        }
    }

    /**
     * Ignore packets when not connected to discord audio gateway
     */
    override fun onStatusChange(status: ConnectionStatus) {
        val connected = status == ConnectionStatus.CONNECTED

        log("discord_audio", "Connection is ${status.name}")
        server.ignoreAudioPackets = !connected

        if (!status.shouldReconnect() && !arrayOf(ConnectionStatus.NOT_CONNECTED, ConnectionStatus.SHUTTING_DOWN).contains(status)) {
            // disconnected unexpectedly and cannot reconnect
            leaveChannel()
            send(Message.error("disconnected unexpectedly from voice channel and cannot reconnect"))
        }
    }

    /**
     * Tell the controller which discord users are speaking
     */
    override fun onUserSpeaking(user: User, speaking: Boolean) {
        if (speaking) speakingUsers.add(user.idLong)
        else speakingUsers.remove(user.idLong)
        val speakingMembers = speakingUsers.map { id -> targetGuild!!.getMemberById(id).effectiveName }
        sendSpeaking(speakingMembers)
    }

    private val speakingUsers: HashSet<Long> = HashSet()

    override fun onPing(ping: Long) {}
}