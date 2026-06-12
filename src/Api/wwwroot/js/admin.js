// ── Auth helper ───────────────────────────────────────────────────────────────
const Auth = {
    init() {
        // Sincroniza token do cookie para localStorage (após login via form)
        const cookieJwt = document.cookie.split(';').map(c => c.trim())
            .find(c => c.startsWith('jwt='));
        if (cookieJwt) {
            const token = decodeURIComponent(cookieJwt.split('=').slice(1).join('='));
            if (token) localStorage.setItem('jwt', token);
        }
    },
    getToken: () => localStorage.getItem('jwt'),
    headers() {
        const t = this.getToken();
        return t ? { 'Authorization': `Bearer ${t}`, 'Content-Type': 'application/json' }
                 : { 'Content-Type': 'application/json' };
    },
    async fetch(url, opts = {}) {
        const res = await fetch(url, { ...opts, headers: { ...this.headers(), ...(opts.headers||{}) } });
        if (res.status === 401) { localStorage.removeItem('jwt'); window.location.href = '/login'; return null; }
        return res;
    },
    async json(url, opts = {}) {
        const res = await this.fetch(url, opts);
        if (!res || !res.ok) return null;
        const ct = res.headers.get('content-type') || '';
        return ct.includes('json') ? res.json() : null;
    }
};
Auth.init();

// ── Sidebar toggle ────────────────────────────────────────────────────────────
const toggle  = document.getElementById('sidebarToggle');
const sidebar = document.getElementById('sidebar');
if (toggle && sidebar) {
    toggle.addEventListener('click', () => sidebar.classList.toggle('open'));
    document.addEventListener('click', e => {
        if (sidebar.classList.contains('open') && !sidebar.contains(e.target) && e.target !== toggle)
            sidebar.classList.remove('open');
    });
}

// ── Alert badge ───────────────────────────────────────────────────────────────
async function loadAlertBadge() {
    const data = await Auth.json('/api/v1/alerts?unreadOnly=true&pageSize=1');
    if (!data) return;
    const badge = document.getElementById('alertBadge');
    if (badge && data.total > 0) {
        badge.textContent = data.total > 99 ? '99+' : data.total;
        badge.style.display = 'inline-flex';
    }
}
loadAlertBadge();

// ── Toast ─────────────────────────────────────────────────────────────────────
function toast(msg, type = 'success') {
    const el = document.createElement('div');
    el.className = `toast toast--${type}`;
    el.innerHTML = `<span class="material-symbols-outlined">${type === 'success' ? 'check_circle' : 'error'}</span> ${msg}`;
    document.body.appendChild(el);
    setTimeout(() => el.remove(), 3500);
}

// ── Format helpers ────────────────────────────────────────────────────────────
const fmtBRL  = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
const fmtDate = d => d ? new Date(d).toLocaleDateString('pt-BR') : '—';
const fmtNum  = n => n != null ? Number(n).toLocaleString('pt-BR') : '—';

// ── Modal ─────────────────────────────────────────────────────────────────────
function openModal(id) {
    const modal = document.getElementById(id);
    if (!modal) return;
    const box = modal.querySelector('.modal__box');
    if (box && !box.querySelector('.modal__close')) {
        const btn = document.createElement('button');
        btn.className = 'modal__close';
        btn.setAttribute('aria-label', 'Fechar');
        btn.innerHTML = '<span class="material-symbols-outlined">close</span>';
        btn.addEventListener('click', () => closeModal(id));
        box.insertAdjacentElement('afterbegin', btn);
    }
    modal.classList.add('open');
}
function closeModal(id) { document.getElementById(id)?.classList.remove('open'); }
document.addEventListener('click', e => {
    if (e.target.classList.contains('modal')) e.target.classList.remove('open');
});
