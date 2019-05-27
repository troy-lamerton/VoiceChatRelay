package com.gamingforgood.discordbot

import java.lang.Exception
import java.util.*
import java.util.concurrent.Phaser
import kotlin.concurrent.schedule

fun log(tag: String, vararg strings: String) {
    println("[$tag] ${strings.joinToString("  ")}")
}

fun Exception.log(withTag: String) {
    log(withTag, this.toString())
}

class Handler: Timer() {
    fun postDelayed(block: TimerTask.() -> Unit, delay: Long) {
        schedule(delay, block)
    }
}