import l from '../common/logger';
import jwt from 'jsonwebtoken'
import agent from 'superagent'
import xml from 'xml2js'

const v_domain = 'vd5.vivox.com'

export default class TokenGenerator {
    // vivox does not state that this can expire
    // however the authToken contains a timestamp
    // that is 15 minutes from now
    private authToken: string
    
    joinChannelToken(channel: string, username: string) {
        let action: VivoxAction = {action: 'join', channel}
        return this.getTokenFor(action, username)
    }
    
    private getTokenFor(action: VivoxAction, username: string): string {
        this.checkUsername(username)
        
        let expiresAfter = 5 // minutes
        let payload: TokenPayload = {
            iss: process.env.V_ISSUER,
            exp: Date.now() + 60 * expiresAfter,
            vxa: action.action,
            vxi: Math.round(Math.random() * 10000),
            f: `sip:.${process.env.V_ISSUER}.${username}.@${v_domain}`,
        }
        
        switch (action.action) {
            case 'join':
            payload.t = `sip:confctl-d-${process.env.V_ISSUER}.${action.channel!}@${v_domain}`
            break
        }
        
        return jwt.sign(payload, process.env.V_SECRET)
    }
    
    async initAdmin() {
        let res = await this.signinAdmin()
        this.authToken = res.auth_token
        l.info(`Set auth token to ${this.authToken}`)
    }
    
    private async signinAdmin() {
        // if (!this.validUsername()) throw new Error(`Invalid char in username ${username}`)
        let res = await vivoxReq('viv_signin', {
            userid: process.env.V_ADMIN_USER,
            pwd: process.env.V_ADMIN_PASSWORD,
            access_token: this.getTokenFor({action: 'login'}, process.env.V_ADMIN_USER)
        })
        return res
    }

    private checkUsername(input: string) {
        let reg = /[a-zA-Z0-9=+\-_\.\!\~\(\)]+/
        let res = reg.exec(input)
        if (!res) throw new Error(`Invalid char in username: ${input}`)
        let invalidCharCount = res[0].length != input.length
        if (invalidCharCount) throw new Error(`${invalidCharCount} invalid characters in username: ${input}`)
    }
}

// used by the admin user
export function vivoxReq(method: string, query: object, log = false): any {
    return new Promise(async (resolve, reject) => {
        const res = await agent
            .post(`https://vdx5.www.vivox.com/api2/${method}.php`)
            .query(query);

        if (res.text.includes('>ERR<')) {
            l.error(res.text)
        } else if (log) {
            l.info(res.text)
        }
        xml.parseString(res.text, (err, json) => {
            if (err) 
                reject(err);
            else
                resolve(json.response.level0[0].body[0]);
        });
    })
}

export type VivoxActionType = 'login' | 'join'
export type VivoxAction = {
    action: VivoxActionType,
    channel?: string
}

type TokenPayload = {
    iss: string
    exp: number
    vxa: VivoxActionType,
    vxi: number,
    sub?: string
    f: string,
    t?: string
}