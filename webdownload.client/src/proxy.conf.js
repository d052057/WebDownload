const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://127.0.0.1:${env.ASPNETCORE_HTTPS_PORT}` :
    env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7115';

const PROXY_CONFIG = [
  {
    context: [
      "/downloadHub"
    ],
    target,
    secure: false,
    ws: true,
  },
  { context: ["/api"], target, secure: false, changeOrigin: true },
  // Your SignalrService hardcodes '/webdownload/downloadHub' as the hub URL
  // (see signalr.service.ts), and the subtitle dashboard's API calls should
  // eventually match that same convention. These two entries cover that
  // case for local `ng serve` too, stripping the prefix before forwarding
  // since the backend itself doesn't know about it.
  {
    context: ["/webdownload/downloadHub"],
    target,
    secure: false,
    ws: true,
    pathRewrite: { "^/webdownload": "" },
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

