package com.gamingforgood.discordbot

import net.dv8tion.jda.core.audio.hooks.ConnectionListener
import net.dv8tion.jda.core.audio.hooks.ConnectionStatus
import net.dv8tion.jda.core.entities.User
import java.net.*

/**
 * Receives audio from vrelay and handles changes in discord audio connection
 */
class UdpServer(port: Int) : Thread() {

    private val bufferToDiscord = ToDiscordBuffer.bufferObject
    private val socket = DatagramSocket(port)
    private var clientIpAddress: InetAddress? = null
    private var clientPort: Int = -1
    private var didWarn = false
    private var running = true

    private val clientIsConnected
        get() = clientIpAddress != null

    var ignoreAudioPackets: Boolean = true
        set(newValue) {
            if (field == newValue) {
                return
            }
            field = newValue
            log("udp", "ignore audio packets -> $newValue")
        }

    fun sendToClient(data: ByteArray) {
        if (!server.clientIsConnected) {
//            log("warn", "No client is connected to udp server, dropping ${data.size} bytes")
            return
        }
        val packet = CreatePacket(data)
        socket.send(packet)
    }

    override fun run() {

        val receiveHelloData = ByteArray(128)
        val receiveHelloPacket = DatagramPacket(receiveHelloData, receiveHelloData.size)
        while (running) {
            log("udp","Udp socket begin - waiting for client")
            socket.receive(receiveHelloPacket)
            val sentence = String(receiveHelloPacket.data).trim()
            if (sentence.startsWith("hello")) {
                log("udp","Client connected")
                break
            } else if (!didWarn) {
                didWarn = true
                log("udp","Client should send 'hello' first! not '$sentence'")
            }
        }
        if (!running) return

        clientIpAddress = receiveHelloPacket.address
        clientPort = receiveHelloPacket.port

        val helloResponse = "HELLO".toByteArray()
        sendToClient(helloResponse)

        // receive data continuously
        log("udp", "Forwarding audio to discord")
        val pcmSample = ByteArray(1920 * 2) // constant set in unity C# project
        val pcmSamplePacket = DatagramPacket(pcmSample, pcmSample.size)

        while (running) {
            if (ignoreAudioPackets) {
                Thread.sleep(10)
                continue
            }
            log("udp", "receiving...")
            socket.receive(pcmSamplePacket) // read bytes from unity into the packet
            log("udp", "write to buffer")
            bufferToDiscord.writeToBuffer(pcmSamplePacket) // write bytes to buffer for JDA to collect
        }

        log("udp", "Server stopped")
    }

    fun disconnect() {
        running = false
    }

    private fun CreatePacket(data: ByteArray): DatagramPacket {
        return DatagramPacket(data, data.size, clientIpAddress, clientPort)
    }
}