import http from 'http'
import WebSocket from 'ws'

const webSocketServer = new WebSocket.Server({
    server: http.createServer(),
    port: parseInt(process.env.PORT_CONTROLLER_WS),
})


export default webSocketServer
