import './common/env';
import './dynamo/configure'
import runManagerServer from './server';

const port = parseInt(process.env.PORT_MANAGER);

runManagerServer(port)

