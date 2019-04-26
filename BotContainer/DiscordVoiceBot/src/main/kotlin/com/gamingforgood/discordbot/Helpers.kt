package com.gamingforgood.discordbot

import java.util.ArrayList
import javax.sound.sampled.*


fun filterDevices(supportedLine: Line.Info): List<Mixer.Info> {
    val result = ArrayList<Mixer.Info>()

    for (info in AudioSystem.getMixerInfo()) {
        val mixer = AudioSystem.getMixer(info)
        if (mixer.isLineSupported(supportedLine)) {
            result.add(info)
        }
    }
    return result
}

fun getMicrophoneLine(format: AudioFormat): TargetDataLine? {
    val captureLine = Line.Info(TargetDataLine::class.java)
    val supportedMicrophones = filterDevices(captureLine)
    require(supportedMicrophones.isNotEmpty())

    for (thisMixerInfo in supportedMicrophones) {
//        if (!thisMixerInfo.name.contains("CABLE Output")) continue
//        println("Mixer info: " + thisMixerInfo.name)

        val line = AudioSystem.getTargetDataLine(format, thisMixerInfo)
        if (thisMixerInfo.name.contains("CABLE")) {
            println("Using ${thisMixerInfo.name}")
            return line
        } else {
//            println("Ignored ${thisMixerInfo.name}")
        }
    }
    return null
}

fun log(tag: String, vararg strings: String) {
    println("[$tag] ${strings.joinToString("  ")}")
}
