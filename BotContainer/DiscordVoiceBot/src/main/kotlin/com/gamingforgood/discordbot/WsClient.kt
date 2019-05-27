package com.gamingforgood.discordbot

import io.socket.client.IO
import io.socket.client.Socket
import org.json.JSONObject
import java.io.IOException
import java.io.RandomAccessFile
import java.util.concurrent.CountDownLatch

abstract class WsClient(private val serverPort: Int, private val prefix: String) {

    private var handler = Handler()

    private var ws = IO.socket("http://127.0.0.1", IO.Options().apply {
        path = prefix
        reconnection = true
        reconnectionAttempts = 10
        reconnectionDelay = 2000L
        port = serverPort
        reconnectionDelayMax = 3000L
    })

    private var waitForPong: CountDownLatch? = null
    var waitForSent: CountDownLatch? = null

    var sendError: String? = null

    init {
        ws.on(Socket.EVENT_MESSAGE) {
            log("ws", "msg:", it.toString())
            val msg = Message.parse(it.first() as String)
            handleMessage(msg)
        }
        ws.on(Socket.EVENT_CONNECT_ERROR) {
            log("ws", "connect err!", it[0].toString())
        }
        ws.on(Socket.EVENT_CONNECT_TIMEOUT) {
            log("ws", "connect timeout!", it.toString())
        }
        ws.on(Socket.EVENT_CONNECT) {
            log("ws", "connect!", it.toString())
        }
        ws.on(Socket.EVENT_DISCONNECT) {
            log("ws", ws.id(), "disconnected", it.toString())
        }
        ws.on(Socket.EVENT_PING) {
            log("ws", ws.id(), "disconnected", it.toString())
        }

        connect(10)
    }

    private fun connect(retriesLeft: Int): Boolean {
        if (retriesLeft < 0) {
            // cant retry, we failed :(
            throw RuntimeException("turn the ws server on pls")
        }
        try {
            ws.open()
            log("ws", "connected? ${ws.connected()}")
            return ws.connected()
        } catch (ex: Exception) {
            ex.log("ws")
            return connect(retriesLeft - 1)
        }
    }

    fun send(message: Message): Boolean {
            waitForSent = CountDownLatch(1)
            ws.send("")

            ws.send(message.toString())

            sendError?.let {
                log("ws", it)
                sendError = null
                return false
            }

            return true
    }

    abstract fun handleMessage(message: Message)
}

class Message(var command: String, val contents: String = "") {

    override fun toString(): String {
        return "$command;$contents"
    }

    companion object {
        fun speaking(namesSpeaking: List<String>?) = Message("speaking", namesSpeaking?.joinToString(",") ?: "_")
        fun info(targetGuildId: String?, targetChannelId: String?) = Message("info", "${targetGuildId ?: "-1"},${targetChannelId ?: "-1"}")
        fun joined(targetGuildId: String, targetChannelId: String) = Message("joined", "$targetGuildId,$targetChannelId")
        fun error(contents: String) = Message("error", contents)
        val left = Message("left")
        fun parse(data: String): Message {
            val parts = data.split(";")
            val command = parts[0]
            val contents = if (parts.size > 1) parts[1].trim() else ""

            return Message(command, contents)
        }
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