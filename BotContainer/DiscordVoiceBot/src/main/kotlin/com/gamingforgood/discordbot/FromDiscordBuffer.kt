package com.gamingforgood.discordbot

import java.util.concurrent.LinkedBlockingQueue

/**
 * Discord bot writes data to this buffer
 * Udp socket reads it and forwards the data to unity dissonance
 */
class FromDiscordBuffer {

    private val QUEUE_CAPACITY = 30
    private val buffer = ByteArray(BUFFER_SIZE)
    private val buffersQueue = LinkedBlockingQueue<ByteArray>(QUEUE_CAPACITY) // x 20ms = length of queue
    private var write_index: Int = 0

    val isDataReady: Boolean
        get() = buffersQueue.size >= 1

    @Synchronized
    fun writeToBuffer(data: ByteArray) {
        for (datum in data) {
            if (write_index >= BUFFER_SIZE) {
                if (buffersQueue.size == QUEUE_CAPACITY) {
                    // drop the oldest buffer
                    buffersQueue.remove()
                    // and also drop this one (don't add it)
                }
                buffersQueue.add(buffer.clone())
                write_index = 0
            }
            buffer[write_index] = datum
            write_index++
        }
    }

    @Synchronized
    fun readDiscordAudio(): ByteArray {
        return buffersQueue.poll()
    }

    companion object {

        internal val BUFFER_SIZE: Int = 3840 // 48000 * 16 * 2 / 8 / (1000/20)
        private var buf: FromDiscordBuffer? = null

        val bufferObject: FromDiscordBuffer
            get() {
                if (buf == null) {
                    buf = FromDiscordBuffer()
                }
                return buf!!
            }
    }

}

