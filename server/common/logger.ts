import pino from 'pino';

const l = pino({
  name: process.env.APP_ID,
  level: process.env.LOG_LEVEL,
  prettyPrint: {
    translateTime: 'HH:MM:ss.L',
    levelFirst: false,
    ignore: 'pid,hostname',
  },
});

export default l;
