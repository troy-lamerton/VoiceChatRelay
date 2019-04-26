import './common/env';
import runServer from './server';
import ip from 'ip'

const port = parseInt(process.env.PORT_CONTROLLER);

console.log('ip is ' + ip.address())

runServer(port)
