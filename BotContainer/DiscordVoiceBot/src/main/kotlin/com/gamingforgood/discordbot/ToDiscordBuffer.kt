package com.gamingforgood.discordbot

import java.net.DatagramPacket
import java.util.concurrent.LinkedBlockingQueue

class ToDiscordBuffer private constructor() {

    init {
        log("buffer", "ToDiscordBuffer rdy")
    }

    private val QUEUE_CAPACITY = 30
    private val buffer = ByteArray(BUFFER_SIZE)
    private val buffersQueue = LinkedBlockingQueue<ByteArray>(QUEUE_CAPACITY) // x 20ms = length of queue
    private var write_index: Int = 0

    val isDataReady: Boolean
        get() = buffersQueue.size >= 2

    var droppedFrames = 0

    internal fun writeToBuffer(packet: DatagramPacket) {
        val data = packet.data
        for (i in packet.offset until packet.length) {
            if (write_index >= BUFFER_SIZE) {
                if (buffersQueue.size == QUEUE_CAPACITY) {
                    // drop the oldest buffer
                    buffersQueue.remove()
                    droppedFrames++
                    if (droppedFrames > 50) {
                        log("buffer", "dropped 1 second of frames")
                        droppedFrames = 0
                    }
                    // and also drop this one (don't add it) // made sound worser quality
                }
                buffersQueue.add(buffer.clone())
                write_index = 0
            }
            buffer[write_index] = data[i]
            write_index++
        }
    }

    @Synchronized
    fun read20MsOfAudio(): ByteArray {
        return buffersQueue.poll()
    }

    companion object {

        // number of bytes in 20ms of 48khz stereo audio
        // as required by AudioSendHandler.INPUT_FORMAT
        internal const val BUFFER_SIZE: Int = 3840 // 48000 * 16 bit-audio * 2 channels / 8 bits-in-a-byte / (1000 ms-in-a-second / 20 ms) = # of bytes in 20ms of audio
        private var buf: ToDiscordBuffer? = null

        val bufferObject: ToDiscordBuffer
            get() {
                if (buf == null) {
                    buf = ToDiscordBuffer()
                }
                return buf!!
            }
    }

}

