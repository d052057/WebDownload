const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://127.0.0.1:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://127.0.0.1:5001';

const PROXY_CONFIG = [
  {
    context: [
      "/downloadHub",
      "/convertHub",
      "/splitterHub",
    ],
    target,
    secure: false,
    ws: true,
    pathRewrite: { "^/webdownload": "" },
  },
  {
    context: ["/api"],
    target,
    secure: false,
    changeOrigin: true
  },
  {
    context: ["/webdownload/api"],
    target,
    secure: false,
    changeOrigin: true,
    pathRewrite: { "^/webdownload": "" },
  },
]

module.exports = PROXY_CONFIG;

