const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT ? `https://127.0.0.1:${env.ASPNETCORE_HTTPS_PORT}` :
  env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://127.0.0.1:5001';

const PROXY_CONFIG = [
  {
    // The app is served under baseHref "/webdownload/", so the client calls the hubs as
    // /webdownload/downloadHub etc. Both forms are listed; pathRewrite strips the prefix.
    context: [
      "/downloadHub",
      "/convertHub",
      "/splitterHub",
      "/voiceSwapHub",
      "/webdownload/downloadHub",
      "/webdownload/convertHub",
      "/webdownload/splitterHub",
      "/webdownload/voiceSwapHub",
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
    // Finished voiceover / splitter files and downloads are served by the server at /medias.
    context: ["/webdownload/medias"],
    target,
    secure: false,
    changeOrigin: true,
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

