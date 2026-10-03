namespace AITool.WebDashboard
{
    /// <summary>
    /// The dashboard's single front-end page: vanilla HTML/CSS/JS, no CDN dependencies, embedded directly
    /// in the assembly so the whole dashboard ships as one small HTTP server with no extra files to deploy.
    /// </summary>
    public static class WebDashboardPage
    {
        public const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>AITool Dashboard</title>
<style>
:root {
  --bg: #f3f4f6;
  --fg: #1b1f23;
  --card-bg: #ffffff;
  --border: #d9dce1;
  --accent: #2f6fed;
  --ok: #1b8a4c;
  --warn: #b4790d;
  --err: #c0392b;
  --muted: #6b7280;
}
@media (prefers-color-scheme: dark) {
  :root {
    --bg: #14161a;
    --fg: #e7e9ee;
    --card-bg: #1d2026;
    --border: #2c3038;
    --accent: #6fa0ff;
    --ok: #3ddc84;
    --warn: #e0a83e;
    --err: #ff6b5f;
    --muted: #9aa1ab;
  }
}
* { box-sizing: border-box; }
body {
  margin: 0;
  background: var(--bg);
  color: var(--fg);
  font-family: -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif;
  font-size: 15px;
}
header {
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 10px;
}
header h1 { font-size: 18px; margin: 0; }
header .sub { color: var(--muted); font-size: 13px; }
main { padding: 12px 16px 40px; max-width: 1100px; margin: 0 auto; }
section { margin-top: 20px; }
section h2 { font-size: 15px; margin: 0 0 8px; color: var(--muted); text-transform: uppercase; letter-spacing: .04em; }
.cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; }
.card {
  background: var(--card-bg);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 12px;
}
.card .big { font-size: 22px; font-weight: 600; }
.card .lbl { color: var(--muted); font-size: 12px; margin-top: 2px; }
table { width: 100%; border-collapse: collapse; background: var(--card-bg); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; }
table caption { text-align: left; }
th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid var(--border); font-size: 13px; white-space: nowrap; }
th { color: var(--muted); font-weight: 600; }
tr:last-child td { border-bottom: none; }
.tablewrap { overflow-x: auto; }
.badge { display: inline-block; padding: 2px 8px; border-radius: 999px; font-size: 12px; font-weight: 600; }
.badge.ok { background: color-mix(in srgb, var(--ok) 20%, transparent); color: var(--ok); }
.badge.warn { background: color-mix(in srgb, var(--warn) 20%, transparent); color: var(--warn); }
.badge.err { background: color-mix(in srgb, var(--err) 20%, transparent); color: var(--err); }
.btn {
  border: 1px solid var(--border);
  background: var(--card-bg);
  color: var(--fg);
  border-radius: 8px;
  padding: 7px 12px;
  font-size: 13px;
  cursor: pointer;
}
.btn:hover { border-color: var(--accent); }
.btn.small { padding: 4px 10px; font-size: 12px; }
.btn.primary { background: var(--accent); color: #fff; border-color: var(--accent); }
.toolbar { display: flex; gap: 8px; margin-bottom: 8px; flex-wrap: wrap; }
.histgrid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
.hist-card { background: var(--card-bg); border: 1px solid var(--border); border-radius: 10px; overflow: hidden; }
.hist-card img { width: 100%; aspect-ratio: 16/9; object-fit: cover; display: block; background: #0002; }
.hist-info { padding: 8px; }
.hist-cam { font-weight: 600; font-size: 13px; }
.hist-date { color: var(--muted); font-size: 11px; }
.hist-det { font-size: 12px; margin-top: 4px; }
.hist-det.success { color: var(--ok); }
#gate {
  position: fixed; inset: 0; background: color-mix(in srgb, var(--bg) 85%, black 15%);
  display: none; align-items: center; justify-content: center; z-index: 50; padding: 16px;
}
#gate .box { background: var(--card-bg); border: 1px solid var(--border); border-radius: 12px; padding: 20px; max-width: 320px; width: 100%; }
#gate h2 { margin-top: 0; text-transform: none; }
#gate input { width: 100%; padding: 8px; border-radius: 8px; border: 1px solid var(--border); background: var(--bg); color: var(--fg); margin: 8px 0 12px; }
footer { text-align: center; color: var(--muted); font-size: 12px; padding: 20px; }
</style>
</head>
<body>

<div id="gate">
  <form class="box" id="gateForm">
    <h2>Enter access token</h2>
    <input id="gateInput" type="password" placeholder="Dashboard token" autocomplete="off">
    <button class="btn primary" type="submit" style="width:100%">Unlock</button>
  </form>
</div>

<header>
  <h1>AITool Dashboard</h1>
  <span class="sub">v<span id="version">-</span> &middot; up <span id="uptime">-</span></span>
</header>

<main>

<section>
  <div class="cards">
    <div class="card"><div class="big" id="imgQueue">-</div><div class="lbl">Image queue</div></div>
    <div class="card"><div class="big" id="actQueue">-</div><div class="lbl">Action queue</div></div>
    <div class="card"><div class="big" id="lastDetection" style="font-size:14px">-</div><div class="lbl">Last detection</div></div>
  </div>
</section>

<section>
  <h2>Cameras</h2>
  <div class="toolbar">
    <button class="btn" id="pauseAllBtn">Pause all</button>
    <button class="btn" id="resumeAllBtn">Resume all</button>
  </div>
  <div class="tablewrap">
  <table>
    <thead><tr><th>Name</th><th>Enabled</th><th>Status</th><th>Last trigger</th><th>Alerts / False / Irrelevant</th><th></th></tr></thead>
    <tbody id="cameraRows"></tbody>
  </table>
  </div>
</section>

<section>
  <h2>AI Servers</h2>
  <div class="tablewrap">
  <table>
    <thead><tr><th>Name</th><th>Type</th><th>Enabled</th><th>Online</th><th>In use</th><th>Avg time</th><th>Errors</th><th>Last result</th></tr></thead>
    <tbody id="serverRows"></tbody>
  </table>
  </div>
</section>

<section>
  <h2>Recent history</h2>
  <div class="histgrid" id="historyGrid"></div>
</section>

</main>
<footer>Auto-refreshes every 5 seconds.</footer>

<script>
(function () {
  var TOKEN_KEY = 'aitool_dashboard_token';
  var token = '';

  function readTokenFromUrlOrStorage() {
    var params = new URLSearchParams(location.search);
    var qToken = params.get('token');

    if (qToken) {
      try { sessionStorage.setItem(TOKEN_KEY, qToken); } catch (e) {}
      params.delete('token');
      var qs = params.toString();
      history.replaceState({}, '', location.pathname + (qs ? ('?' + qs) : ''));
    }

    try { return sessionStorage.getItem(TOKEN_KEY) || ''; } catch (e) { return ''; }
  }

  function setToken(t) {
    token = t;
    try { sessionStorage.setItem(TOKEN_KEY, t); } catch (e) {}
  }

  function apiUrl(path) {
    var sep = path.indexOf('?') >= 0 ? '&' : '?';
    return path + sep + 'token=' + encodeURIComponent(token);
  }

  function api(path, options) {
    options = options || {};
    options.headers = options.headers || {};
    options.headers['Authorization'] = 'Bearer ' + token;

    return fetch(path, options).then(function (res) {
      if (res.status === 401) {
        showGate();
        throw new Error('unauthorized');
      }
      return res.json();
    });
  }

  function showGate() { document.getElementById('gate').style.display = 'flex'; }
  function hideGate() { document.getElementById('gate').style.display = 'none'; }

  function el(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text !== undefined) e.textContent = text;
    return e;
  }

  function fmtDuration(seconds) {
    seconds = Math.floor(seconds || 0);
    var d = Math.floor(seconds / 86400); seconds %= 86400;
    var h = Math.floor(seconds / 3600); seconds %= 3600;
    var m = Math.floor(seconds / 60);
    var parts = [];
    if (d) parts.push(d + 'd');
    if (d || h) parts.push(h + 'h');
    parts.push(m + 'm');
    return parts.join(' ');
  }

  function fmtDate(iso) {
    if (!iso) return '-';
    var d = new Date(iso);
    if (isNaN(d.getTime())) return '-';
    return d.toLocaleString();
  }

  function pauseResume(camera, pause) {
    api(pause ? '/api/pause' : '/api/resume', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ camera: camera })
    }).then(refreshAll).catch(function () {});
  }

  function renderStatus(s) {
    document.getElementById('version').textContent = s.Version || '-';
    document.getElementById('uptime').textContent = fmtDuration(s.UptimeSeconds);
    document.getElementById('imgQueue').textContent = s.ImageQueueLength;
    document.getElementById('actQueue').textContent = s.ActionQueueLength;
    document.getElementById('lastDetection').textContent = fmtDate(s.LastDetectionTime);

    var tbody = document.getElementById('cameraRows');
    tbody.innerHTML = '';

    (s.Cameras || []).forEach(function (c) {
      var tr = document.createElement('tr');
      tr.appendChild(el('td', '', c.Name));
      tr.appendChild(el('td', '', c.Enabled ? 'Yes' : 'No'));

      var statusTd = el('td');
      statusTd.appendChild(el('span', c.Paused ? 'badge warn' : 'badge ok', c.Paused ? 'Paused' : 'Active'));
      tr.appendChild(statusTd);

      tr.appendChild(el('td', '', fmtDate(c.LastTriggerTime)));
      tr.appendChild(el('td', '', c.StatsAlerts + ' / ' + c.StatsFalseAlerts + ' / ' + c.StatsIrrelevantAlerts));

      var btnTd = el('td');
      var btn = el('button', 'btn small', c.Paused ? 'Resume' : 'Pause');
      btn.addEventListener('click', function () { pauseResume(c.Name, !c.Paused); });
      btnTd.appendChild(btn);
      tr.appendChild(btnTd);

      tbody.appendChild(tr);
    });
  }

  function renderServers(list) {
    var tbody = document.getElementById('serverRows');
    tbody.innerHTML = '';

    (list || []).forEach(function (srv) {
      var tr = document.createElement('tr');
      tr.appendChild(el('td', '', srv.Name));
      tr.appendChild(el('td', '', srv.Type));
      tr.appendChild(el('td', '', srv.Enabled ? 'Yes' : 'No'));

      var onlineTd = el('td');
      onlineTd.appendChild(el('span', srv.Online ? 'badge ok' : 'badge err', srv.Online ? 'Online' : 'Offline'));
      tr.appendChild(onlineTd);

      tr.appendChild(el('td', '', srv.InUse ? 'Yes' : 'No'));
      tr.appendChild(el('td', '', srv.AvgTimeMS + ' ms'));
      tr.appendChild(el('td', '', String(srv.ErrCount)));
      tr.appendChild(el('td', '', srv.LastResultMessage || ''));
      tbody.appendChild(tr);
    });
  }

  function renderHistory(list) {
    var grid = document.getElementById('historyGrid');
    grid.innerHTML = '';

    (list || []).forEach(function (h) {
      var card = el('div', 'hist-card');

      var img = document.createElement('img');
      img.loading = 'lazy';
      img.src = apiUrl(h.AnnotatedImageUrl || h.ImageUrl);
      img.alt = h.Camera;
      card.appendChild(img);

      var info = el('div', 'hist-info');
      info.appendChild(el('div', 'hist-cam', h.Camera));
      info.appendChild(el('div', 'hist-date', fmtDate(h.Date)));
      info.appendChild(el('div', h.Success ? 'hist-det success' : 'hist-det', h.Detections || '(no detections)'));
      card.appendChild(info);

      grid.appendChild(card);
    });
  }

  function refreshAll() {
    api('/api/status').then(renderStatus).catch(function () {});
    api('/api/servers').then(renderServers).catch(function () {});
    api('/api/history?limit=50').then(renderHistory).catch(function () {});
  }

  document.addEventListener('DOMContentLoaded', function () {
    document.getElementById('gateForm').addEventListener('submit', function (e) {
      e.preventDefault();
      setToken(document.getElementById('gateInput').value.trim());
      hideGate();
      refreshAll();
    });

    document.getElementById('pauseAllBtn').addEventListener('click', function () { pauseResume('all', true); });
    document.getElementById('resumeAllBtn').addEventListener('click', function () { pauseResume('all', false); });

    token = readTokenFromUrlOrStorage();

    if (!token) {
      showGate();
    } else {
      refreshAll();
    }

    setInterval(refreshAll, 5000);
  });
})();
</script>
</body>
</html>
""";
    }
}
