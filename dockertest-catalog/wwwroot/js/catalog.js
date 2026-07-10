const fileInput = document.getElementById('file-input');
const uploadZone = document.getElementById('upload-zone');
const pickFileBtn = document.getElementById('pick-file');
const selectedFileEl = document.getElementById('selected-file');
const versionInput = document.getElementById('version-input');
const nameInput = document.getElementById('name-input');
const uploadBtn = document.getElementById('upload-btn');
const uploadProgressWrap = document.getElementById('upload-progress-wrap');
const uploadProgress = document.getElementById('upload-progress');
const uploadStatus = document.getElementById('upload-status');
const releasesList = document.getElementById('releases-list');
const refreshBtn = document.getElementById('refresh-btn');

let selectedFile = null;

function formatBytes(bytes) {
    if (!bytes) return '—';
    const units = ['B', 'KB', 'MB', 'GB'];
    let i = 0;
    let n = bytes;
    while (n >= 1024 && i < units.length - 1) {
        n /= 1024;
        i++;
    }
    return `${n.toFixed(i === 0 ? 0 : 1)} ${units[i]}`;
}

function setSelectedFile(file) {
    selectedFile = file;
    uploadBtn.disabled = !file;
    uploadZone.classList.toggle('has-file', !!file);
    selectedFileEl.textContent = file
        ? `${file.name} (${formatBytes(file.size)})`
        : 'Henüz dosya seçilmedi';

    const match = file?.name.match(/dockertest-(\d+\.\d+\.\d+)\.tar\.gz/i);
    if (match && !versionInput.value)
        versionInput.value = match[1];
}

pickFileBtn.addEventListener('click', (e) => {
    e.preventDefault();
    fileInput.click();
});

uploadZone.addEventListener('click', (e) => {
    if (e.target === pickFileBtn) return;
    fileInput.click();
});

fileInput.addEventListener('change', () => {
    setSelectedFile(fileInput.files[0] ?? null);
});

uploadZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    uploadZone.classList.add('dragover');
});

uploadZone.addEventListener('dragleave', () => uploadZone.classList.remove('dragover'));

uploadZone.addEventListener('drop', (e) => {
    e.preventDefault();
    uploadZone.classList.remove('dragover');
    const file = e.dataTransfer.files[0];
    if (file) setSelectedFile(file);
});

async function loadReleases() {
    const res = await fetch('/api/releases');
    const data = await res.json();
    const items = data.items ?? [];

    if (!items.length) {
        releasesList.innerHTML = `<p class="text-muted">${data.hint || 'Henüz sürüm yok. Soldan .tar.gz yükle.'}</p>`;
        return;
    }

    releasesList.innerHTML = items.map(r => `
        <div class="version-row">
            <div>
                <strong>v${r.version.replace(/^v/, '')}</strong>
                <div class="text-muted small">${r.name}</div>
                <div class="text-muted small">${formatBytes(r.fileSizeBytes)} · ${new Date(r.publishedAt).toLocaleString('tr-TR')}</div>
            </div>
            <div class="d-flex gap-2 align-items-center">
                <a class="btn btn-sm btn-outline-light" href="${r.downloadUrl}" download>İndir</a>
                <button class="btn btn-sm btn-outline-danger" onclick="deleteRelease('${r.version}')">Sil</button>
            </div>
        </div>
    `).join('');
}

async function uploadRelease() {
    if (!selectedFile) return;

    const form = new FormData();
    form.append('file', selectedFile);
    if (versionInput.value.trim()) form.append('version', versionInput.value.trim());
    if (nameInput.value.trim()) form.append('name', nameInput.value.trim());

    uploadBtn.disabled = true;
    uploadProgressWrap.classList.remove('d-none');
    uploadStatus.classList.remove('d-none');
    uploadStatus.textContent = 'Yükleniyor... (büyük dosyalarda uzun sürebilir)';
    uploadProgress.style.width = '30%';

    try {
        const res = await fetch('/api/releases/upload', { method: 'POST', body: form });
        const data = await res.json().catch(() => ({}));

        if (!res.ok) {
            uploadStatus.textContent = data.error || 'Yükleme başarısız.';
            uploadProgress.classList.add('bg-danger');
            return;
        }

        uploadProgress.style.width = '100%';
        uploadProgress.classList.remove('progress-bar-animated');
        uploadStatus.textContent = data.message || 'Yüklendi!';
        setSelectedFile(null);
        fileInput.value = '';
        versionInput.value = '';
        nameInput.value = '';
        await loadReleases();
    } catch (err) {
        uploadStatus.textContent = `Hata: ${err.message}`;
        uploadProgress.classList.add('bg-danger');
    } finally {
        uploadBtn.disabled = !selectedFile;
        setTimeout(() => {
            uploadProgressWrap.classList.add('d-none');
            uploadProgress.style.width = '0%';
            uploadProgress.classList.remove('bg-danger', 'progress-bar-animated');
            uploadProgress.classList.add('progress-bar-animated');
            uploadStatus.classList.add('d-none');
        }, 3000);
    }
}

async function deleteRelease(version) {
    if (!confirm(`v${version} silinsin mi?`)) return;
    const res = await fetch(`/api/releases/${version}`, { method: 'DELETE' });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) {
        alert(data.error || 'Silinemedi.');
        return;
    }
    await loadReleases();
}

uploadBtn.addEventListener('click', uploadRelease);
refreshBtn.addEventListener('click', loadReleases);
window.deleteRelease = deleteRelease;

loadReleases();
