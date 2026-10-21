/* Desktop wrapper for the original aBowman hamster. The SWF is unmodified. */
window.RufflePlayer = window.RufflePlayer || {};
window.RufflePlayer.config = {
  publicPath: 'ruffle/', autoplay: 'on', unmuteOverlay: 'hidden',
  splashScreen: false, preloader: false, contextMenu: 'off',
  showSwfDownload: false, allowScriptAccess: false,
  openUrlMode: 'deny', allowNetworking: 'none', logLevel: 'error'
};

const notify = message => window.chrome?.webview?.postMessage(message);
let player;
window.setHamsterPaused = paused => {
  if (!player) return;
  const api = player.ruffle();
  if (paused) api.suspend(); else api.resume();
};
window.getHamsterStatus = () => ({
  ready: document.body.dataset.ready === 'true',
  playing: player?.ruffle().isPlaying ?? false,
  metadata: player?.ruffle().metadata ?? null,
  resources: performance.getEntriesByType('resource').map(item => item.name),
  error: document.body.dataset.error || null
});
document.addEventListener('contextmenu', event => {
  event.preventDefault();
  notify('menu');
}, true);
window.addEventListener('error', event => notify('error:' + event.message));
window.addEventListener('DOMContentLoaded', async () => {
  const loading = document.getElementById('loading');
  try {
    player = window.RufflePlayer.newest().createPlayer();
    document.getElementById('stage').appendChild(player);
    await player.ruffle().load({
      url: 'hamster.swf', backgroundColor: '#FFFFFF',
      parameters: {
        up_bgColor: 'FFFFFF', up_bodyColor: 'E6DEBE', up_earColor: 'D4C898',
        up_snoutColor: 'F7F4E9', up_eyeColor: '000000', up_feetColor: 'D4C898',
        up_tailColor: 'E6DEBE', up_waterColor: 'E0EFFF', up_foodColor: 'CBA920',
        up_wheelColor: 'FFFFFF', up_wheelOuterColor: 'FF4D4D',
        up_wheelCenterColor: 'E4EB2F', up_wheelSpokeColor: 'DEDEDE'
      }
    });
    loading.hidden = true;
    document.body.dataset.ready = 'true';
    notify('ready');
  } catch (error) {
    document.body.dataset.error = String(error);
    loading.textContent = '小窝打开失败，请从菜单重新加载。';
    notify('error:' + String(error));
  }
});
