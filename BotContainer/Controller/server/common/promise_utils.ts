import { EventEmitter } from "events";
import l from '../common/logger'

export function eventPromise<T extends EventEmitter>(emitter: T, successEvent: string, failureEvent: string, timeout: number = 3000): Promise<boolean> {
    return new Promise((resolve, reject) => {
        function success() {
            clearTimeout(enforceTimeout)
            emitter.removeListener(failureEvent, failure)
            resolve(true)
        }
        function failure() {
            clearTimeout(enforceTimeout)
            emitter.removeListener(successEvent, success)
            resolve(false)
        }
        emitter.once(successEvent, success)
        emitter.once(failureEvent, failure)

        function timeoutFailure() {
            l.debug(`timeout waiting for ${emitter.constructor.name} to emit '${successEvent}'`)
            emitter.removeListener(successEvent, success)
            emitter.removeListener(failureEvent, failure)
            resolve(false)
        }
        let enforceTimeout = setTimeout(timeoutFailure, timeout)
    })
}

export function eventPromiseFiltered<T extends EventEmitter, T2>(emitter: T, targetEvent: string, filter: (a: T2) => boolean, timeout: number = 3000): Promise<T2> {
    return new Promise((resolve, reject) => {
        let enforceTimeout = setTimeout(() => {
            l.debug(`timed out waiting for ${emitter.constructor.name} to emit '${targetEvent}'`)
            emitter.removeListener(targetEvent, listener)
            resolve(null)
        }, timeout)

        function listener(a: T2) {
            if (filter(a)) {
                clearTimeout(enforceTimeout)
                emitter.removeListener(targetEvent, listener)
                resolve(a)
            }
        }
        emitter.on(targetEvent, listener)
    })
}