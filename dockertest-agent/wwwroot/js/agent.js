const phases = [
    'StoppingCurrent', 'PullingImage', 'StartingProduction', 'HealthCheckingProduction', 'Completed'
];

const phaseOrder = Object.fromEntries(phases.map((p, i) => [p, i]));

let activeVersion = null;
let isBusy = false;

const progressBar = document.getElementById('progress-bar');
const progressMessage = document.getElementById('progress-message');
const logBox = document.getElementById('log-box');
const releasesList = document.getElementById('releases-list');
const containersList = document.getElementById('containers-list');
const activeBadge = document.getElementById('active-version-badge');

function appendLog(msg) {
    const line = document.createElement('div');
    line.textContent = `[${new Date().toLocaleTimeString()}] ${msg}`;
    logBox.appendChild(line);
    logBox.scrollTop = logBox.scrollHeight;
}

function updatePhases(currentPhase, failed) {
    const currentIdx = phaseOrder[currentPhase] ?? -1;
    document.querySelectorAll('#phase-list .phase-step').forEach(el => {
        const p = el.dataset.phase;
        const idx = phaseOrder[p] ?? 99;
        el.classList.remove('active', 'done', 'failed');
        if (failed && p === currentPhase) el.classList.add('failed');
        else if (idx < currentIdx) el.classList.add('done');
        else if (idx === currentIdx) el.classList.add('active');
    });
}

function applyProgress(p) {
    if (!p) return;
    progressBar.style.width = `${p.percent || 0}%`;
    progressMessage.textContent = p.message || 'Hazır';
    isBusy = p.isRunning;
    const failed = p.phase === 'Failed' || p.phase === 'RollingBack';
    if (p.phase === 'RolledBack') updatePhases('Completed', false);
    else if (phases.includes(p.phase)) updatePhases(p.phase, failed);
}

async function loadStatus() {
    const res = await fetch('/api/status');
    const data = await res.json();
    activeVersion = data.activeVersion;
    activeBadge.textContent = `Uygulama: ${data.activeVersion || '—'}`;
    applyProgress(data.progress);
    isBusy = data.isBusy;
}

async function loadReleases() {
    const res = await fetch('/api/releases');
    const data = await res.json();
    const releases = data.items ?? data;

    if (!releases.length) {
        const hint = data.hint || data.error || 'Release bulunamadı.';
        const isCatalog = data.source === 'catalog';
        const isRateLimit = (hint + (data.error || '')).toLowerCase().includes('rate limit');
        const tokenNote = isCatalog
            ? '<br><br><strong>Çözüm:</strong> Catalog paneline git (8090), <code>dockertest-x.y.z.tar.gz</code> yükle.'
            : isRateLimit
            ? `<br><br><strong>Çözüm:</strong> GitHub PAT oluştur ve agent'a ver:<br>
               <code>-e Agent__GitHubToken=ghp_xxx</code><br>
               <small>Settings → Developer settings → PAT → public_repo işaretle</small>`
            : (data.tokenConfigured
                ? '<br><small>Token tanımlı ama liste boş.</small>'
                : '<br><small>İsteğe bağlı: <code>-e Agent__GitHubToken=ghp_xxx</code></small>');
        releasesList.innerHTML = `<p class="text-muted">${hint}${tokenNote}</p>`;
        return;
    }

    const sourceLabels = {
        'catalog': 'Release Kataloğu (.tar.gz)',
        'github-releases': 'GitHub Releases',
        'github-tags': 'GitHub Tags',
        'github-packages': 'GHCR Packages',
        'docker-local': 'Yerel Docker image'
    };
    const sourceNote = data.source
        ? `<p class="text-muted small mb-2">Kaynak: ${sourceLabels[data.source] || data.source}</p>`
        : '';
    releasesList.innerHTML = sourceNote + releases.map(r => {
        const isCurrent = r.version === activeVersion;
        return `
        <div class="version-row ${isCurrent ? 'current' : ''}">
            <div>
                <strong>v${r.version.replace(/^v/, '')}</strong>
                <div class="text-muted small">${r.name}</div>
            </div>
            <button class="btn btn-sm ${isCurrent ? 'btn-secondary' : 'btn-accent'}"
                ${isCurrent || isBusy ? 'disabled' : ''}
                onclick="deploy('${r.version}')">
                ${isCurrent ? 'Aktif' : 'Geç'}
            </button>
        </div>`;
    }).join('');
}

async function loadContainers() {
    const res = await fetch('/api/containers');
    const containers = await res.json();
    if (!containers.length) {
        containersList.innerHTML = '<p class="text-muted">Henüz container yok.</p>';
        return;
    }
    containersList.innerHTML = containers.map(c => `
        <div class="version-row ${c.isActive ? 'current' : ''} mb-2">
            <div>
                <strong>${c.name}</strong>
                <div class="text-muted small">${c.state} · port ${c.hostPort ?? '—'}</div>
            </div>
            ${c.isActive
                ? '<span class="badge badge-active">Aktif</span>'
                : `<button class="btn btn-sm btn-outline-accent" ${isBusy ? 'disabled' : ''} onclick="rollback('${c.name}')">Geri Dön</button>`}
        </div>
    `).join('');
}

async function deploy(version) {
    if (!confirm(`v${version} sürümüne geçilsin mi?`)) return;
    logBox.innerHTML = '';
    appendLog(`Güncelleme başlatılıyor: v${version}`);
    await fetch(`/api/deploy/${version}`, { method: 'POST' });
    await refresh();
}

async function rollback(name) {
    if (!confirm(`${name} container'ına geri dönülsün mü?`)) return;
    logBox.innerHTML = '';
    appendLog(`Rollback başlatılıyor: ${name}`);
    await fetch(`/api/rollback/${name}`, { method: 'POST' });
    await refresh();
}

async function refresh() {
    await loadStatus();
    await Promise.all([loadReleases(), loadContainers()]);
}

const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/update')
    .withAutomaticReconnect()
    .build();

connection.on('progress', applyProgress);
connection.on('log', appendLog);

connection.start().then(refresh);
setInterval(refresh, 10000);

window.deploy = deploy;
window.rollback = rollback;
