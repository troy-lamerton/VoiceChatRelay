import AWS from 'aws-sdk'

AWS.config.secretAccessKey = 'local'
AWS.config.accessKeyId = 'local';

(AWS.config.update as any)({
  region: 'us-west-2',
  endpoint: 'http://localhost:8000'
});
