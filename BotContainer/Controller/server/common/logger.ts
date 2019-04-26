import pino from 'pino';

const l = pino({
  level: process.env.LOG_LEVEL,
  prettyPrint: {
    translateTime: 'HH:MM:ss.L',
    levelFirst: false,
    ignore: 'pid,hostname',
  },
  customLevels: {
    // info is 30
    _dbot: 31,
    _vrelay: 32
    // warn is 40
  },
});
l.dbot = (msg) => {
  l._dbot('DBOT |', msg)
}
l.vrelay = (msg) => {
  l._vrelay('VRELAY |', msg)
}

export default l;

export function intArrayToString(ints: number[]) {
  let str = ''
  for (const i in ints){
      str += String.fromCharCode(ints[i])
  }
  return str
}