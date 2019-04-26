package com.gamingforgood.discordbot

import java.io.IOException
import java.io.RandomAccessFile

abstract class PipeClient(private val pipesPrefix: String) : Thread("PipeClient_$pipesPrefix") {

    private lateinit var writePipe: NamedPipe
    private lateinit var readPipe: NamedPipe

    override fun run() {
        try {
            // Connect to the writePipe
            readPipe = NamedPipe("${pipesPrefix}_server")
            writePipe = NamedPipe("${pipesPrefix}_client")
        } catch (e: Exception) {
            e.printStackTrace()
            return
        }

        while (readPipe.connected) {
            // read commands
            val line = readPipe.readLine()
            if (line.isNullOrEmpty()) {
                Thread.sleep(10)
                continue
            }
//            log(writePipe.name, "received: $line")
            val parts = line.split(";")
            val command = parts[0]
            val contents = if (parts.size > 1) parts[1].trim() else ""

            val message = Message(command, contents)
            onReceive(message)
        }

        log(readPipe.name, "disconnected")
    }

    fun send(message: Message): Boolean {
        return writePipe.send(message)
    }

    fun ping(): Boolean {
        return writePipe.ping()
    }

    abstract fun handleMessage(message: Message)

    private fun onReceive(message: Message) {
        when (message.command) {
            "ping" -> {
//                log(readPipe.name, "server ping")
                writePipe.pong()
            }
            else -> handleMessage(message)
        }
    }

    private fun close(status: Int) {
        readPipe.close()
        writePipe.close()
    }
}

class Message(var command: String, val contents: String) {
    constructor(command: String): this(command, "")

    companion object {
        fun speaking(namesSpeaking: List<String>?) = Message("speaking", namesSpeaking?.joinToString(",") ?: "_")
        fun info(targetGuildId: String?, targetChannelId: String?) = Message("info", "${targetGuildId ?: "-1"},${targetChannelId ?: "-1"}")
        fun joined(targetGuildId: String, targetChannelId: String) = Message("joined", "$targetGuildId,$targetChannelId")
        fun error(contents: String) = Message("error", contents)
        val left = Message("left")
    }
}

class NamedPipe(val name: String) : RandomAccessFile("\\\\.\\pipe\\$name", "rw") {
    var connected: Boolean = true
        private set

    fun send(command: String, data: String): Boolean {
//        log(name, "send [$command]: [$data]")
        return writeString("$command;$data")
    }

    fun send(command: String): Boolean {
        return send(command, "")
    }

    fun send(message: Message): Boolean {
        return send(message.command, message.contents)
    }

    fun ping(): Boolean {
        return send("ping")
    }

    fun pong(): Boolean {
        return send("pong")
    }

    private fun writeString(string: String): Boolean {
        try {
            write(string.toByteArray())
            return true
        } catch (ex: IOException) {
            if (ex.message?.contains("pipe is being closed") == true) {
                // this pipe cannot be recovered
            } else {
                ex.printStackTrace()
            }
            this.close()
            System.runFinalization()
        }
        return false
    }

    override fun close() {
        connected = false
        try {
            super.close()
        } catch (ex: IOException) {
            ex.printStackTrace()
        } finally {
            log("writePipe", "closed writePipe '$name'")
        }
    }
}