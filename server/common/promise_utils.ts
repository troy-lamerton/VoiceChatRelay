import { EventEmitter } from "events";
import l from '../common/logger'

export function eventPromise<T extends EventEmitter>(emitter: T, successEvent: string, failureEvent: string, timeout: number = 3000): Promise<boolean> {
    return new Promise((resolve, reject) => {
        function success() {
            clearTimeout(enforceTimeout)
            emitter.off(failureEvent, failure)
            resolve(true)
        }
        function failure() {
            clearTimeout(enforceTimeout)
            emitter.off(successEvent, success)
            resolve(false)
        }
        emitter.once(successEvent, success)
        emitter.once(failureEvent, failure)

        let enforceTimeout = setTimeout(() => {
            l.debug(`timed out waiting for ${emitter.constructor.name} to emit '${successEvent}'`)
            emitter.off(successEvent, success)
            emitter.off(failureEvent, failure)
            resolve(false)
        }, timeout)
    })
}

export const timeout = (ms: number) => new Promise(res => setTimeout(res, ms))