const API = '/api';

// ═══════════════════════════════════════════════════════════════════════════
//  STATE
// ═══════════════════════════════════════════════════════════════════════════
let currentUser = null;
let allAlumni = [];
let allAlumniRows = [];
let allPending = [];
let filteredAlumni = [];
let filteredPending = [];
let alumniPage = 1;
const PAGE = 20;
let editingRowIdx = null;
let currentSubId = null;
let chartInstances = {};
let allExamResults = [];
let editingExamId = null; // null = add, number = edit
// ── TOKEN KEY ─────────────────────────────────────────────────────────────
// Single source of truth — always localStorage, never a module variable.
// This avoids any scoping issue where the in-memory `token` var is null
// even after saveToken() was called.
const TOKEN_KEY = 'slsu_admin_token';

function saveToken(t) {
    localStorage.setItem(TOKEN_KEY, t);
    console.log('[Token] Saved to localStorage. Length:', t?.length, 'First20:', t?.substring(0, 20));
}
function clearToken() { localStorage.removeItem(TOKEN_KEY); }
function getToken() {
    const t = localStorage.getItem(TOKEN_KEY);
    if (!t) return null;
    // Guard: must be a real JWT (3 dot-separated parts)
    if (t.split('.').length !== 3) {
        console.warn('[Token] Bad value in localStorage, clearing:', t.substring(0, 30));
        clearToken();
        return null;
    }
    return t;
}

// ═══════════════════════════════════════════════════════════════════════════
//  UTILITIES
// ═══════════════════════════════════════════════════════════════════════════
async function apiFetch(path, opts = {}) {
    const t = getToken();
    if (!t) {
        console.warn('[apiFetch] No valid token for:', path);
        return { ok: false, status: 401, data: null };
    }

    const method = (opts.method || 'GET').toUpperCase();
    const headers = {
        'Authorization': `Bearer ${t}`,
        ...(opts.body ? { 'Content-Type': 'application/json' } : {}),
        ...(opts.headers || {})
    };

    console.log(`[apiFetch] ${method} ${path} | token[0:20]: ${t.substring(0, 20)}`);

    try {
        const res = await fetch(API + path, { ...opts, headers });
        console.log(`[apiFetch] ${method} ${path} → ${res.status}`);
        const data = res.headers.get('content-type')?.includes('json')
            ? await res.json() : null;
        return { ok: res.ok, status: res.status, data };
    } catch (e) {
        console.error('[apiFetch] Network error:', path, e.message);
        return { ok: false, status: 0, data: null };
    }
}

async function publicFetch(path) {
    try {
        const res = await fetch(API + path);
        return res.ok ? await res.json() : null;
    } catch { return null; }
}

function openModal(id) { document.getElementById(id).classList.add('open'); document.body.style.overflow = 'hidden'; }
function closeModal(id) { document.getElementById(id).classList.remove('open'); document.body.style.overflow = ''; }

document.querySelectorAll('.modal-overlay').forEach(o => {
    o.addEventListener('click', e => { if (e.target === o) closeModal(o.id); });
});
document.addEventListener('keydown', e => {
    if (e.key === 'Escape')
        document.querySelectorAll('.modal-overlay.open').forEach(m => closeModal(m.id));
});

function toast(msg, type = '') {
    const el = document.getElementById('toast');
    el.textContent = msg;
    el.className = `show ${type}`;
    setTimeout(() => el.classList.remove('show', type), 3000);
}

function setText(id, val) {
    const el = document.getElementById(id);
    if (el) el.textContent = val;
}

function fmtDate(iso) {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-PH',
        { year: 'numeric', month: 'short', day: 'numeric' });
}

function killChart(id) {
    if (chartInstances[id]) { chartInstances[id].destroy(); delete chartInstances[id]; }
}

// ═══════════════════════════════════════════════════════════════════════════
//  AUTH
// ═══════════════════════════════════════════════════════════════════════════
async function doLogin() {
    const username = document.getElementById('l-user').value.trim();
    const password = document.getElementById('l-pass').value;
    const errEl = document.getElementById('login-err');
    errEl.style.display = 'none';

    if (!username || !password) {
        errEl.textContent = 'Please enter username and password.';
        errEl.style.display = 'block';
        return;
    }

    // Wipe any old/bad token first
    clearToken();

    let r, data;
    try {
        r = await fetch(API + '/auth/login', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });
        data = await r.json();
    } catch (e) {
        errEl.textContent = 'Could not connect to server. Is the API running?';
        errEl.style.display = 'block';
        return;
    }

    if (!r.ok) {
        errEl.textContent = data?.message || `Login failed (HTTP ${r.status}).`;
        errEl.style.display = 'block';
        return;
    }

    if (!data?.token) {
        errEl.textContent = 'Server did not return a token.';
        errEl.style.display = 'block';
        return;
    }

    // Validate the token format before saving
    const parsed = parseJwt(data.token);
    if (!parsed) {
        errEl.textContent = 'Server returned an invalid token format.';
        errEl.style.display = 'block';
        return;
    }

    saveToken(data.token);  // save to localStorage under TOKEN_KEY
    currentUser = parsed;
    console.log('[JWT] Logged in as:', currentUser.username, '| Role:', currentUser.role);
    console.log('[JWT] Token in storage after save:', getToken()?.substring(0, 20));

    await bootApp();
}

async function verifyToken() {
    const t = getToken(); // validates format, clears if bad
    if (!t) return false;

    const parsed = parseJwt(t);
    if (!parsed) { clearToken(); return false; }

    // Check expiry
    try {
        const payload = JSON.parse(atob(t.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));
        if (payload.exp && payload.exp < Math.floor(Date.now() / 1000)) {
            console.warn('[JWT] Token expired.');
            clearToken();
            return false;
        }
    } catch { clearToken(); return false; }

    currentUser = parsed;
    console.log('[JWT] Session restored for:', currentUser.username);
    return true;
}

function logout() {
    clearToken();
    currentUser = null;
    document.getElementById('login-screen').style.display = 'flex';
}

// ═══════════════════════════════════════════════════════════════════════════
//  BOOT
// ═══════════════════════════════════════════════════════════════════════════
async function bootApp() {
    document.getElementById('login-screen').style.display = 'none';

    setText('admin-name', currentUser.fullName || currentUser.username || 'Admin');
    setText('admin-role', currentUser.role || 'admin');

    const avatar = document.getElementById('admin-avatar');
    if (avatar) {
        avatar.textContent =
            (currentUser.fullName || currentUser.username || 'A')[0].toUpperCase();
    }

    if (currentUser.role === 'superadmin') {
        const lbl = document.getElementById('admin-mgmt-label');
        const nav = document.getElementById('nav-accounts');
        if (lbl) lbl.style.display = 'block';
        if (nav) nav.style.display = 'flex';
    }

    // Final token check before loading data
    const t = getToken();
    console.log('[bootApp] Token check before loading:', t ? 'PRESENT (' + t.length + ' chars)' : 'MISSING');
    if (!t) { console.error('[bootApp] Aborting — no token.'); return; }

    await loadDashboard();
    await loadAlumniData();
    await loadPending();
}

// ═══════════════════════════════════════════════════════════════════════════
//  NAVIGATION
// ═══════════════════════════════════════════════════════════════════════════
const viewTitles = {
    dashboard: 'Dashboard',
    alumni: 'Alumni Records',
    pending: 'Pending Approvals',
    board: 'Board Exam Data',
    employment: 'Employment Data',
    accounts: 'Admin Accounts',
    examresults: 'Exam Results Management'
};

function switchView(name) {
    document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
    document.getElementById(`view-${name}`).classList.add('active');
    document.querySelector(`.nav-item[data-view="${name}"]`).classList.add('active');
    setText('topbar-title', viewTitles[name]);

    if (name === 'board') loadBoardView();
    if (name === 'employment') loadEmploymentView();
    if (name === 'accounts') loadAccounts();
    if (name === 'examresults') loadExamResults();
}

// ═══════════════════════════════════════════════════════════════════════════
//  DASHBOARD
// ═══════════════════════════════════════════════════════════════════════════
async function loadDashboard() {
    const [summary, rme, grad, emp, ind, pendingResult] = await Promise.all([
        publicFetch('/alumni/analytics/summary'),
        publicFetch('/alumni/analytics/rme-passing-rate'),
        publicFetch('/alumni/analytics/graduates-per-year'),
        publicFetch('/alumni/analytics/employment-breakdown'),
        publicFetch('/alumni/analytics/industry-breakdown'),
        apiFetch('/submissions/count')
    ]);

    const pendingCount = pendingResult.data?.pending ?? 0;

    if (summary) {
        setText('d-total', summary.totalAlumni);
        setText('d-employed', summary.totalEmployed);
        setText('d-rme', summary.totalRmePassers);
    }

    setText('d-pending', pendingCount);
    updatePendingBadge(pendingCount);

    if (grad) buildBarChart('c-grad', Object.keys(grad), Object.values(grad), '#183356', 'Graduates');
    if (emp) buildDoughnut('c-emp', Object.keys(emp), Object.values(emp));
    if (ind) {
        const top8 = Object.entries(ind).slice(0, 8);
        buildHorizBar('c-ind', top8.map(e => e[0]), top8.map(e => e[1]));
    }
    if (rme?.byYear?.length) {
        buildStackedBar('c-rme',
            rme.byYear.map(y => y.year),
            rme.byYear.map(y => y.passers),
            rme.byYear.map(y => y.takers - y.passers)
        );
    }
}

function updatePendingBadge(count) {
    const badge = document.getElementById('pending-badge');
    if (!badge) return;
    if (count > 0) { badge.textContent = count; badge.style.display = 'inline-block'; }
    else { badge.style.display = 'none'; }
    setText('d-pending', count);
}

// ═══════════════════════════════════════════════════════════════════════════
//  ALUMNI RECORDS (CRUD)
// ═══════════════════════════════════════════════════════════════════════════
async function loadAlumniData() {
    const data = await publicFetch('/alumni');
    if (!data) return;
    allAlumni = data;
    allAlumniRows = data.map((a, i) => ({ ...a, _rowIdx: i + 1 }));
    filteredAlumni = [...allAlumniRows];
    buildEmpFilter();
    renderAlumniTable();
}

function buildEmpFilter() {
    const sel = document.getElementById('alumni-emp-filter');
    if (!sel) return;
    while (sel.options.length > 1) sel.remove(1);
    const emps = [...new Set(allAlumni.map(a => a.employmentType).filter(Boolean))];
    emps.forEach(e => {
        const o = document.createElement('option');
        o.value = e; o.textContent = e;
        sel.appendChild(o);
    });
}

function filterAlumni() {
    const kw = document.getElementById('alumni-search')?.value.toLowerCase().trim() ?? '';
    const yr = document.getElementById('alumni-year-search')?.value.trim() ?? '';
    const emp = document.getElementById('alumni-emp-filter')?.value ?? '';

    filteredAlumni = allAlumniRows.filter(a => {
        const mKw = !kw || (a.fullName || '').toLowerCase().includes(kw)
            || (a.companyName || '').toLowerCase().includes(kw);
        const mYr = !yr || (a.yearGraduated || '') === yr;
        const mEmp = !emp || a.employmentType === emp;
        return mKw && mYr && mEmp;
    });

    const pill = document.getElementById('year-filter-pill');
    const pillVal = document.getElementById('year-pill-value');
    if (yr && pill && pillVal) { pill.style.display = 'block'; pillVal.textContent = yr; }
    else if (pill) { pill.style.display = 'none'; }

    alumniPage = 1;
    renderAlumniTable();
}

function clearYearFilter() {
    const input = document.getElementById('alumni-year-search');
    if (input) input.value = '';
    filterAlumni();
}

function renderAlumniTable() {
    const total = filteredAlumni.length;
    const pages = Math.ceil(total / PAGE);
    const start = (alumniPage - 1) * PAGE;
    const slice = filteredAlumni.slice(start, start + PAGE);
    const tbody = document.getElementById('alumni-tbody');

    tbody.innerHTML = slice.length === 0
        ? `<tr class="load-row"><td colspan="10">No records found.</td></tr>`
        : slice.map((a, i) => {
            const rc = a.passedLicensureExam === 'Yes' ? 'pass' : 'fail';
            const rt = a.passedLicensureExam === 'Yes' ? 'Passed' : 'Not Passed';
            return `<tr>
                <td style="color:var(--gt)">${start + i + 1}</td>
                <td style="font-weight:600">${a.fullName || '—'}</td>
                <td>${a.yearGraduated || '—'}</td>
                <td>${a.jobTitle || '—'}</td>
                <td>${a.companyName || '—'}</td>
                <td>${a.industry || '—'}</td>
                <td>${a.employmentType || '—'}</td>
                <td><span class="badge ${rc}">${rt}</span></td>
                <td>${a.passerStatus || '—'}</td>
                <td>
                    <div class="tbl-actions">
                        <button class="btn-tbl btn-edit"
                            onclick="openEditAlumni(${a._rowIdx})">
                            <i class="fa-solid fa-pen"></i>
                        </button>
                        <button class="btn-tbl btn-del"
                            onclick="confirmDeleteAlumni(${a._rowIdx}, '${(a.fullName || '').replace(/'/g, "\\'")}')">
                            <i class="fa-solid fa-trash"></i>
                        </button>
                    </div>
                </td>
            </tr>`;
        }).join('');

    setText('alumni-info',
        `Showing ${Math.min(start + 1, total)}–${Math.min(start + PAGE, total)} of ${total} records`);

    const pager = document.getElementById('alumni-pager');
    pager.innerHTML = '';
    if (pages > 1) {
        const mk = (lbl, p, disabled = false) => {
            const b = document.createElement('button');
            b.innerHTML = lbl; b.disabled = disabled;
            if (p === alumniPage) b.classList.add('active');
            b.onclick = () => { alumniPage = p; renderAlumniTable(); };
            return b;
        };
        pager.appendChild(mk('‹', alumniPage - 1, alumniPage === 1));
        for (let p = Math.max(1, alumniPage - 2); p <= Math.min(pages, alumniPage + 2); p++)
            pager.appendChild(mk(p, p));
        pager.appendChild(mk('›', alumniPage + 1, alumniPage === pages));
    }
}

function openAddAlumni() {
    editingRowIdx = null;
    setText('modal-alumni-title', 'Add Alumni');
    clearAlumniForm();
    openModal('modal-alumni');
}

function openEditAlumni(rowIdx) {
    editingRowIdx = rowIdx;
    const a = allAlumniRows.find(r => r._rowIdx === rowIdx);
    if (!a) return;
    setText('modal-alumni-title', 'Edit Alumni');
    setVal('f-name', a.fullName);
    setVal('f-sex', a.sex);
    setVal('f-dob', a.dateOfBirth);
    setVal('f-email', a.email || a.emailAddress);
    setVal('f-contact', a.contactNumber);
    setVal('f-address', a.presentAddress);
    setVal('f-enrolled', a.yearEnrolled);
    setVal('f-graduated', a.yearGraduated);
    setVal('f-grad-prog', a.graduateSchoolProgram);
    setVal('f-rme', a.passedLicensureExam);
    setVal('f-month-taken', a.monthTaken);
    setVal('f-year-taken', a.yearTaken);
    setVal('f-passer', a.passerStatus);
    setVal('f-awards', a.awards);
    setVal('f-jobtitle', a.jobTitle);
    setVal('f-company', a.companyName);
    setVal('f-industry', a.industry);
    setVal('f-emptype', a.employmentType);
    setVal('f-jobloc', a.jobLocation);
    openModal('modal-alumni');
}

async function saveAlumni() {
    const fullName = document.getElementById('f-name').value.trim();
    if (!fullName) { toast('Full name is required.', 'error'); return; }

    const payload = {
        timestamp: new Date().toLocaleDateString('en-PH'),
        emailAddress: gv('f-email'),
        email: gv('f-email'),
        agreement: 'I Agree',
        fullName,
        sex: gv('f-sex'),
        dateOfBirth: gv('f-dob'),
        presentAddress: gv('f-address'),
        contactNumber: gv('f-contact'),
        yearEnrolled: gv('f-enrolled'),
        yearGraduated: gv('f-graduated'),
        graduateSchoolProgram: gv('f-grad-prog'),
        passedLicensureExam: gv('f-rme'),
        monthTaken: gv('f-month-taken'),
        yearTaken: gv('f-year-taken'),
        passerStatus: gv('f-passer'),
        awards: gv('f-awards'),
        jobTitle: gv('f-jobtitle'),
        companyName: gv('f-company'),
        industry: gv('f-industry'),
        employmentType: gv('f-emptype'),
        jobLocation: gv('f-jobloc'),
        privacyConsent: 'Yes'
    };

    const res = editingRowIdx === null
        ? await apiFetch('/alumni', { method: 'POST', body: JSON.stringify(payload) })
        : await apiFetch(`/alumni/${editingRowIdx}`, { method: 'PUT', body: JSON.stringify(payload) });

    if (res.ok) {
        toast(editingRowIdx ? 'Record updated!' : 'Alumni added!', 'success');
        closeModal('modal-alumni');
        await loadAlumniData();
    } else {
        toast(res.data?.message || 'Failed to save.', 'error');
    }
}

function confirmDeleteAlumni(rowIdx, name) {
    document.getElementById('confirm-msg').textContent =
        `Are you sure you want to delete "${name}"? This permanently removes the row from Google Sheets.`;
    document.getElementById('confirm-ok-btn').onclick = () => deleteAlumni(rowIdx);
    openModal('modal-confirm');
}

async function deleteAlumni(rowIdx) {
    const res = await apiFetch(`/alumni/${rowIdx}`, { method: 'DELETE' });
    closeModal('modal-confirm');
    if (res.ok) { toast('Record deleted.', 'success'); await loadAlumniData(); }
    else toast('Failed to delete.', 'error');
}

function clearAlumniForm() {
    ['f-name', 'f-sex', 'f-dob', 'f-email', 'f-contact', 'f-address',
        'f-enrolled', 'f-graduated', 'f-grad-prog',
        'f-rme', 'f-month-taken', 'f-year-taken', 'f-passer', 'f-awards',
        'f-jobtitle', 'f-company', 'f-industry', 'f-emptype', 'f-jobloc'
    ].forEach(id => { const el = document.getElementById(id); if (el) el.value = ''; });
}

// ═══════════════════════════════════════════════════════════════════════════
//  PENDING SUBMISSIONS
// ═══════════════════════════════════════════════════════════════════════════
async function loadPending() {
    const { ok, status, data } = await apiFetch('/submissions/all');

    if (!ok) {
        console.error('[Pending] Failed:', status, data);
        const tbody = document.getElementById('pending-tbody');
        if (tbody) tbody.innerHTML = `
            <tr class="load-row"><td colspan="8" style="color:var(--red);">
                <i class="fa-solid fa-triangle-exclamation" style="margin-right:6px;"></i>
                Could not load submissions (HTTP ${status}).
            </td></tr>`;
        return;
    }

    allPending = data || [];
    filteredPending = allPending.filter(s => s.status === 'Pending');
    renderPendingTable();
    updatePendingBadge(allPending.filter(s => s.status === 'Pending').length);
    console.log(`[Pending] Loaded ${allPending.length} total`);
}

function filterPending() {
    const status = document.getElementById('pend-filter').value;
    filteredPending = status ? allPending.filter(s => s.status === status) : allPending;
    renderPendingTable();
}

function renderPendingTable() {
    const tbody = document.getElementById('pending-tbody');
    setText('pending-info', `${filteredPending.length} submission(s)`);

    if (!filteredPending.length) {
        tbody.innerHTML = `<tr class="load-row"><td colspan="8">
            <i class="fa-solid fa-inbox" style="margin-right:6px;color:var(--gt);"></i>
            No submissions found.</td></tr>`;
        return;
    }

    tbody.innerHTML = filteredPending.map((s, i) => {
        const sc = s.status.toLowerCase();
        const isPending = s.status === 'Pending';
        const safeName = (s.fullName || '').replace(/'/g, "\\'");
        return `<tr>
            <td style="color:var(--gt)">${i + 1}</td>
            <td style="font-weight:600">${s.fullName || '—'}</td>
            <td>${s.yearGraduated || '—'}</td>
            <td>${s.jobTitle || '—'}</td>
            <td>${s.companyName || '—'}</td>
            <td>${fmtDate(s.submittedAt)}</td>
            <td><span class="badge ${sc}">${s.status}</span></td>
            <td><div class="tbl-actions">
                <button class="btn-tbl btn-view" onclick="viewSubmission(${s.id})">
                    <i class="fa-solid fa-eye"></i> View
                </button>
                ${isPending ? `
                <button class="btn-tbl btn-approve" onclick="quickApprove(${s.id})">
                    <i class="fa-solid fa-check"></i>
                </button>
                <button class="btn-tbl btn-reject" onclick="quickReject(${s.id})">
                    <i class="fa-solid fa-xmark"></i>
                </button>` : ''}
                <button class="btn-tbl btn-del" onclick="confirmDeleteSub(${s.id},'${safeName}')">
                    <i class="fa-solid fa-trash"></i>
                </button>
            </div></td>
        </tr>`;
    }).join('');
}

function viewSubmission(id) {
    const s = allPending.find(p => p.id === id);
    if (!s) return;
    currentSubId = id;
    setText('sub-modal-title', `Submission — ${s.fullName}`);

    const fields = [
        ['Full Name', s.fullName], ['Sex', s.sex], ['Date of Birth', s.dateOfBirth],
        ['Email', s.email], ['Contact Number', s.contactNumber], ['Present Address', s.presentAddress],
        ['Year Enrolled', s.yearEnrolled], ['Year Graduated', s.yearGraduated],
        ['Grad. Program', s.graduateSchoolProgram], ['Passed RME?', s.passedLicensureExam],
        ['Month Taken', s.monthTaken], ['Year Taken', s.yearTaken],
        ['Passer Status', s.passerStatus], ['Awards', s.awards],
        ['Job Title', s.jobTitle], ['Company', s.companyName], ['Industry', s.industry],
        ['Employment Type', s.employmentType], ['Job Location', s.jobLocation],
        ['Submitted', fmtDate(s.submittedAt)], ['Status', s.status],
        ['Reviewed By', s.reviewedBy || '—'], ['Rejection Reason', s.rejectionReason || '—']
    ];

    document.getElementById('sub-detail-grid').innerHTML = fields.map(([l, v]) =>
        `<div class="detail-item"><div class="dl">${l}</div><div class="dv">${v || '—'}</div></div>`
    ).join('');

    const isPending = s.status === 'Pending';
    document.getElementById('btn-approve-sub').style.display = isPending ? 'inline-flex' : 'none';
    document.getElementById('btn-reject-sub').style.display = isPending ? 'inline-flex' : 'none';
    openModal('modal-submission');
}

async function quickApprove(id) { currentSubId = id; await approveCurrentSub(); }
async function quickReject(id) { currentSubId = id; promptReject(); }

async function approveCurrentSub() {
    if (!currentSubId) return;
    const res = await apiFetch(`/submissions/${currentSubId}/approve`, { method: 'POST' });
    closeModal('modal-submission');
    if (res.ok) {
        toast('Approved and added to Google Sheets!', 'success');
        await loadPending();
        await loadAlumniData();
    } else toast(res.data?.message || 'Approval failed.', 'error');
}

function promptReject() {
    closeModal('modal-submission');
    document.getElementById('reject-reason').value = '';
    openModal('modal-reject');
}
async function triggerSync() {
    const btn = document.getElementById('btn-sync');
    if (btn) {
        btn.disabled = true;
        btn.textContent = 'Syncing...';
    }

    const res = await apiFetch('/sync', { method: 'POST' });

    if (btn) {
        btn.disabled = false;
        btn.textContent = 'Sync from Google Sheets';
    }

    if (res.ok) {
        const d = res.data;
        toast(
            d.imported > 0
                ? `✓ ${d.imported} new submission(s) moved to pending!`
                : 'Sync complete — no new submissions.',
            d.imported > 0 ? 'success' : ''
        );
        // Reload pending list to show the new submissions
        await loadPending();
    } else {
        toast('Sync failed. Check the server console.', 'error');
    }
}
async function confirmReject() {
    const reason = document.getElementById('reject-reason').value.trim();
    const res = await apiFetch(`/submissions/${currentSubId}/reject`, {
        method: 'POST', body: JSON.stringify({ reason })
    });
    closeModal('modal-reject');
    if (res.ok) { toast('Submission rejected.', ''); await loadPending(); }
    else toast(res.data?.message || 'Rejection failed.', 'error');
}

function confirmDeleteSub(id, name) {
    document.getElementById('confirm-msg').textContent = `Delete submission from "${name}"?`;
    document.getElementById('confirm-ok-btn').onclick = async () => {
        const res = await apiFetch(`/submissions/${id}`, { method: 'DELETE' });
        closeModal('modal-confirm');
        if (res.ok) { toast('Deleted.', 'success'); await loadPending(); }
        else toast('Delete failed.', 'error');
    };
    openModal('modal-confirm');
}

// ═══════════════════════════════════════════════════════════════════════════
//  BOARD EXAM VIEW
// ═══════════════════════════════════════════════════════════════════════════
async function loadBoardView() {
    const data = await publicFetch('/alumni/analytics/rme-passing-rate');
    if (!data) return;

    setText('b-takers', data.totalTakers);
    setText('b-passers', data.totalPassers);
    setText('b-rate', data.overallPassingRate + '%');

    if (data.byYear?.length) {
        killChart('c-board-detail');
        const ctx = document.getElementById('c-board-detail').getContext('2d');
        chartInstances['c-board-detail'] = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: data.byYear.map(y => y.year),
                datasets: [
                    { label: 'Passers', data: data.byYear.map(y => y.passers), backgroundColor: '#16a34a', borderRadius: 4 },
                    { label: 'Not Passed', data: data.byYear.map(y => y.takers - y.passers), backgroundColor: '#dc2626', borderRadius: 4 }
                ]
            },
            options: { responsive: true, scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true } }, plugins: { legend: { position: 'top' } } }
        });

        document.getElementById('board-tbody').innerHTML = data.byYear.map(y => {
            const monthList = y.byMonth?.length
                ? y.byMonth.map(m =>
                    `<span style="display:inline-block;padding:2px 8px;margin:2px;border-radius:12px;
                     font-size:10px;font-weight:600;background:#EFF6FF;color:#1d4ed8;">
                     ${m.month} (${m.passingRate}%)</span>`).join('')
                : '<span style="color:var(--gt);font-size:11px;">—</span>';

            const awardeeCount = allAlumni.length
                ? allAlumni.filter(a => a.yearTaken === String(y.year) &&
                    a.passedLicensureExam === 'Yes' && a.awards?.trim()).length : 0;

            const awardeeBadge = awardeeCount > 0
                ? `<span style="display:inline-flex;align-items:center;gap:4px;background:#fef9c3;
                   color:#92400e;font-size:11px;font-weight:700;padding:3px 10px;border-radius:20px;cursor:pointer;"
                   onclick="showBoardAwardees('${y.year}')">
                   <i class="fa-solid fa-trophy" style="font-size:10px;"></i>
                   ${awardeeCount} awardee${awardeeCount !== 1 ? 's' : ''}</span>`
                : '<span style="color:var(--gt);font-size:11px;">—</span>';

            return `<tr>
                <td style="font-weight:600">${y.year}</td>
                <td>${y.takers}</td><td>${y.passers}</td><td>${y.takers - y.passers}</td>
                <td><strong style="color:${y.passingRate >= 75 ? '#16a34a' : y.passingRate >= 50 ? '#f59e0b' : '#dc2626'}">${y.passingRate}%</strong></td>
                <td>${monthList}</td><td>${awardeeBadge}</td>
            </tr>`;
        }).join('');
    }
    buildAllAwardeesPanel();
}

function showBoardAwardees(year) {
    buildAllAwardeesPanel(year);
    document.getElementById('board-awards-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

function buildAllAwardeesPanel(filterYear = null) {
    const panel = document.getElementById('board-awards-panel');
    if (!panel || !allAlumni.length) { if (panel) panel.style.display = 'none'; return; }

    const awardees = allAlumni.filter(a =>
        a.passedLicensureExam === 'Yes' && a.awards?.trim() &&
        (!filterYear || a.yearTaken === String(filterYear))
    );

    if (!awardees.length) { panel.innerHTML = ''; panel.style.display = 'none'; return; }

    panel.style.display = 'block';
    panel.innerHTML = `
        <div class="card-head" style="display:flex;align-items:center;justify-content:space-between;">
            <h3 style="display:flex;align-items:center;gap:8px;">
                <i class="fa-solid fa-trophy" style="color:#f59e0b;"></i>
                ${filterYear ? `Awardees — Exam Year ${filterYear}` : 'All Awardees'}
                <span style="background:var(--g2);color:var(--gt);font-size:11px;font-weight:600;
                             padding:2px 10px;border-radius:20px;margin-left:4px;">${awardees.length}</span>
            </h3>
            ${filterYear ? `<button class="btn btn-outline" style="font-size:12px;padding:5px 12px;"
                onclick="buildAllAwardeesPanel()"><i class="fa-solid fa-xmark"></i> Clear Filter</button>` : ''}
        </div>
        <div class="table-wrap"><table>
            <thead><tr><th>#</th><th>Full Name</th><th>Batch Year</th><th>Exam Year</th>
            <th>Month Taken</th><th>Passer Status</th><th>Award / Recognition</th></tr></thead>
            <tbody>${awardees.map((a, i) => `<tr>
                <td style="color:var(--gt)">${i + 1}</td>
                <td style="font-weight:600">${a.fullName || '—'}</td>
                <td>${a.yearGraduated || '—'}</td><td>${a.yearTaken || '—'}</td>
                <td>${a.monthTaken || '—'}</td><td>${a.passerStatus || '—'}</td>
                <td><span style="display:inline-flex;align-items:center;gap:5px;background:#fef9c3;
                    color:#92400e;font-size:11px;font-weight:700;padding:3px 10px;border-radius:20px;">
                    <i class="fa-solid fa-star" style="font-size:9px;color:#f59e0b;"></i>${a.awards}
                </span></td>
            </tr>`).join('')}</tbody>
        </table></div>`;
}

// ═══════════════════════════════════════════════════════════════════════════
//  EMPLOYMENT VIEW
// ═══════════════════════════════════════════════════════════════════════════
async function loadEmploymentView() {
    const [emp, ind] = await Promise.all([
        publicFetch('/alumni/analytics/employment-breakdown'),
        publicFetch('/alumni/analytics/industry-breakdown')
    ]);

    if (emp) {
        killChart('c-emp2');
        chartInstances['c-emp2'] = new Chart(document.getElementById('c-emp2').getContext('2d'), {
            type: 'doughnut',
            data: {
                labels: Object.keys(emp), datasets: [{
                    data: Object.values(emp),
                    backgroundColor: ['#16a34a', '#2563EB', '#f59e0b', '#dc2626', '#7c3aed'], borderWidth: 2
                }]
            },
            options: { responsive: true, plugins: { legend: { position: 'bottom' } } }
        });
    }

    if (ind) {
        const top8 = Object.entries(ind).slice(0, 8);
        killChart('c-ind2');
        chartInstances['c-ind2'] = new Chart(document.getElementById('c-ind2').getContext('2d'), {
            type: 'bar',
            data: { labels: top8.map(e => e[0]), datasets: [{ data: top8.map(e => e[1]), backgroundColor: '#183356', borderRadius: 4 }] },
            options: { indexAxis: 'y', responsive: true, plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true } } }
        });

        const total = Object.values(ind).reduce((a, b) => a + b, 0) || 1;
        document.getElementById('industry-tbody').innerHTML =
            Object.entries(ind).map(([name, cnt]) =>
                `<tr><td style="font-weight:600">${name}</td><td>${cnt}</td><td>${Math.round(cnt / total * 100)}%</td></tr>`
            ).join('');
    }
}

// ═══════════════════════════════════════════════════════════════════════════
//  ADMIN ACCOUNTS
// ═══════════════════════════════════════════════════════════════════════════
async function loadAccounts() {
    const { ok, data } = await apiFetch('/auth/admins');
    if (!ok || !data) return;

    document.getElementById('accounts-tbody').innerHTML = data.map((a, i) => `
        <tr>
            <td style="color:var(--gt)">${i + 1}</td>
            <td style="font-weight:600">${a.fullName}</td>
            <td>${a.username}</td>
            <td><span class="badge ${a.role}">${a.role}</span></td>
            <td><span class="badge ${a.isActive ? 'active' : 'inactive'}">${a.isActive ? 'Active' : 'Inactive'}</span></td>
            <td>${fmtDate(a.createdAt)}</td>
            <td><div class="tbl-actions">
                ${a.role !== 'superadmin' ? `
                <button class="btn-tbl btn-edit" onclick="toggleAdmin(${a.id})">${a.isActive ? 'Deactivate' : 'Activate'}</button>
                <button class="btn-tbl btn-del" onclick="confirmDeleteAdmin(${a.id},'${a.username}')">
                    <i class="fa-solid fa-trash"></i></button>`
            : '<span style="color:var(--gt);font-size:11px;">Protected</span>'}
            </div></td>
        </tr>`).join('');
}

function openCreateAdmin() { openModal('modal-create-admin'); }

async function createAdmin() {
    const fullName = document.getElementById('na-fullname').value.trim();
    const username = document.getElementById('na-username').value.trim();
    const password = document.getElementById('na-password').value;
    const role = document.getElementById('na-role').value;

    if (!fullName || !username || !password) { toast('All fields are required.', 'error'); return; }
    if (password.length < 8) { toast('Password must be at least 8 characters.', 'error'); return; }

    const res = await apiFetch('/auth/admins', { method: 'POST', body: JSON.stringify({ fullName, username, password, role }) });
    if (res.ok) { toast('Admin account created!', 'success'); closeModal('modal-create-admin'); await loadAccounts(); }
    else toast(res.data?.message || 'Failed to create account.', 'error');
}

async function toggleAdmin(id) {
    const res = await apiFetch(`/auth/admins/${id}/toggle`, { method: 'PATCH' });
    if (res.ok) { toast('Status updated.', 'success'); await loadAccounts(); }
    else toast('Failed.', 'error');
}

function confirmDeleteAdmin(id, username) {
    document.getElementById('confirm-msg').textContent = `Delete admin account "${username}"? This cannot be undone.`;
    document.getElementById('confirm-ok-btn').onclick = async () => {
        const res = await apiFetch(`/auth/admins/${id}`, { method: 'DELETE' });
        closeModal('modal-confirm');
        if (res.ok) { toast('Admin deleted.', 'success'); await loadAccounts(); }
        else toast('Failed.', 'error');
    };
    openModal('modal-confirm');
}

// ═══════════════════════════════════════════════════════════════════════════
//  CHART HELPERS
// ═══════════════════════════════════════════════════════════════════════════
function buildBarChart(id, labels, data, color, label) {
    killChart(id);
    chartInstances[id] = new Chart(document.getElementById(id).getContext('2d'), {
        type: 'bar', data: { labels, datasets: [{ label, data, backgroundColor: color, borderRadius: 4 }] },
        options: { responsive: true, plugins: { legend: { display: false } }, scales: { y: { beginAtZero: true } } }
    });
}
function buildDoughnut(id, labels, data) {
    killChart(id);
    chartInstances[id] = new Chart(document.getElementById(id).getContext('2d'), {
        type: 'doughnut', data: { labels, datasets: [{ data, backgroundColor: ['#16a34a', '#2563EB', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2'], borderWidth: 2 }] },
        options: { responsive: true, plugins: { legend: { position: 'bottom' } } }
    });
}
function buildHorizBar(id, labels, data) {
    killChart(id);
    chartInstances[id] = new Chart(document.getElementById(id).getContext('2d'), {
        type: 'bar', data: { labels, datasets: [{ data, backgroundColor: '#1e4080', borderRadius: 4 }] },
        options: { indexAxis: 'y', responsive: true, plugins: { legend: { display: false } }, scales: { x: { beginAtZero: true } } }
    });
}
function buildStackedBar(id, labels, passers, failed) {
    killChart(id);
    chartInstances[id] = new Chart(document.getElementById(id).getContext('2d'), {
        type: 'bar',
        data: {
            labels, datasets: [
                { label: 'Passers', data: passers, backgroundColor: '#16a34a', borderRadius: 4 },
                { label: 'Not Passed', data: failed, backgroundColor: '#dc2626', borderRadius: 4 }
            ]
        },
        options: { responsive: true, scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true } }, plugins: { legend: { position: 'top' } } }
    });
}

// ═══════════════════════════════════════════════════════════════════════════
//  SMALL HELPERS
// ═══════════════════════════════════════════════════════════════════════════
function gv(id) { return document.getElementById(id)?.value?.trim() || ''; }
function setVal(id, val) { const el = document.getElementById(id); if (el) el.value = val || ''; }

function parseJwt(tokenStr) {
    try {
        const parts = tokenStr.split('.');
        if (parts.length !== 3) { console.warn('[JWT] Not a valid JWT — parts:', parts.length); return null; }
        const json = decodeURIComponent(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'))
            .split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
        const payload = JSON.parse(json);
        return {
            username: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || payload['unique_name'] || payload['name'] || '',
            fullName: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname'] || payload['given_name'] || payload['fullName'] || '',
            role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload['role'] || 'admin'
        };
    } catch (e) { console.error('[JWT] Parse failed:', e.message); return null; }
}
// ═══════════════════════════════════════════════════════════════
//  ADD these functions to admin.js
//  Exam Results Management — admin side
// ═══════════════════════════════════════════════════════════════

// Add 'examresults' to viewTitles object:
// examresults: 'Exam Results Management'

// Add to switchView():
// if (name === 'examresults') loadExamResults();

// ── STATE ─────────────────────────────────────────────────────


// ── LOAD & RENDER TABLE ───────────────────────────────────────
async function loadExamResults() {
    const { ok, data } = await apiFetch('/examresults/all');
    if (!ok || !data) return;

    allExamResults = data;

    const tbody = document.getElementById('examresults-tbody');
    if (!tbody) return;

    if (!data.length) {
        tbody.innerHTML = `<tr class="load-row"><td colspan="8">
            No exam results yet. Click "Add Exam Result" to create one.</td></tr>`;
        return;
    }

    tbody.innerHTML = data.map((e, i) => {
        const diffColor = e.differenceFromNational >= 0 ? '#16a34a' : '#dc2626';
        const diffSign = e.differenceFromNational >= 0 ? '+' : '';
        return `<tr>
            <td style="font-weight:600">${e.month} ${e.year}</td>
            <td><span class="badge ${e.dataSource === 'system' ? 'active' : 'pending'}">
                ${e.dataSource}</span></td>
            <td><strong>${e.slsuPassingRate}%</strong></td>
            <td>${e.slsuPassers} / ${e.slsuExaminees}</td>
            <td>${e.nationalPassingRate}%</td>
            <td style="color:${diffColor};font-weight:600;">
                ${diffSign}${e.differenceFromNational}%</td>
            <td><span class="badge ${e.isPublished ? 'pass' : 'inactive'}">
                ${e.isPublished ? 'Published' : 'Draft'}</span></td>
            <td><div class="tbl-actions">
                <button class="btn-tbl btn-edit" onclick="openEditExamResult(${e.id})">
                    <i class="fa-solid fa-pen"></i>
                </button>
                <button class="btn-tbl btn-${e.isPublished ? 'reject' : 'approve'}"
                    onclick="togglePublishExamResult(${e.id})">
                    <i class="fa-solid fa-${e.isPublished ? 'eye-slash' : 'eye'}"></i>
                    ${e.isPublished ? 'Unpublish' : 'Publish'}
                </button>
                <button class="btn-tbl btn-del" onclick="confirmDeleteExamResult(${e.id}, '${e.month} ${e.year}')">
                    <i class="fa-solid fa-trash"></i>
                </button>
            </div></td>
        </tr>`;
    }).join('');
}

// ── OPEN ADD MODAL ────────────────────────────────────────────
function openAddExamResult() {
    editingExamId = null;
    setText('modal-examresult-title', 'Add Exam Result');
    clearExamResultForm();
    openModal('modal-examresult');
}

// ── OPEN EDIT MODAL ───────────────────────────────────────────
function openEditExamResult(id) {
    editingExamId = id;
    const e = allExamResults.find(r => r.id === id);
    if (!e) return;

    setText('modal-examresult-title', `Edit — ${e.month} ${e.year}`);

    // Set data source radio
    document.getElementById('ds-manual').checked = e.dataSource !== 'system';
    document.getElementById('ds-system').checked = e.dataSource === 'system';
    onDataSourceChange(e.dataSource === 'system' ? 'system' : 'manual');

    setVal('er-month', e.month);
    setVal('er-year', e.year);
    setVal('er-slsu-examinees', e.slsuExaminees);
    setVal('er-slsu-passers', e.slsuPassers);
    setVal('er-ft-examinees', e.firstTimeExaminees);
    setVal('er-ft-passers', e.firstTimePassers);
    setVal('er-rep-examinees', e.repeaterExaminees);
    setVal('er-rep-passers', e.repeaterPassers);
    setVal('er-nat-examinees', e.nationalExaminees);
    setVal('er-nat-passers', e.nationalPassers);
    document.getElementById('er-published').checked = e.isPublished;

    autoCalcRates();
    openModal('modal-examresult');
}

// ── DATA SOURCE TOGGLE ────────────────────────────────────────
function onDataSourceChange(value) {
    const pullControls = document.getElementById('system-pull-controls');
    if (pullControls) {
        pullControls.style.display = value === 'system' ? 'block' : 'none';
    }
}

// ── PULL FROM SYSTEM DATA ─────────────────────────────────────
async function pullSystemData() {
    const month = document.getElementById('pull-month').value;
    const year = document.getElementById('pull-year').value;

    if (!month || !year) { toast('Select a month and year first.', 'error'); return; }

    const { ok, data } = await apiFetch(
        `/examresults/preview-system?month=${month}&year=${year}`);

    if (!ok || !data) { toast('Failed to pull system data.', 'error'); return; }

    setVal('er-month', data.month);
    setVal('er-year', data.year);
    setVal('er-slsu-examinees', data.slsuExaminees);
    setVal('er-slsu-passers', data.slsuPassers);
    setVal('er-ft-examinees', data.firstTimeExaminees);
    setVal('er-ft-passers', data.firstTimePassers);
    setVal('er-rep-examinees', data.repeaterExaminees);
    setVal('er-rep-passers', data.repeaterPassers);

    autoCalcRates();
    toast(`Pulled ${data.slsuExaminees} examinees from system. Enter national data manually.`, 'success');
}

// ── AUTO-CALCULATE RATES & NARRATIVE ─────────────────────────
function autoCalcRates() {
    const calc = (passers, examinees) => {
        const p = parseInt(passers) || 0;
        const e = parseInt(examinees) || 0;
        return e > 0 ? (p / e * 100).toFixed(2) : '0.00';
    };

    const slsuRate = calc(
        document.getElementById('er-slsu-passers').value,
        document.getElementById('er-slsu-examinees').value);
    const ftRate = calc(
        document.getElementById('er-ft-passers').value,
        document.getElementById('er-ft-examinees').value);
    const repRate = calc(
        document.getElementById('er-rep-passers').value,
        document.getElementById('er-rep-examinees').value);
    const natRate = calc(
        document.getElementById('er-nat-passers').value,
        document.getElementById('er-nat-examinees').value);

    setVal('er-slsu-rate', slsuRate + '%');
    setVal('er-ft-rate', ftRate + '%');
    setVal('er-rep-rate', repRate + '%');
    setVal('er-nat-rate', natRate + '%');

    // Update narrative preview
    const month = document.getElementById('er-month').value;
    const year = document.getElementById('er-year').value;
    const preview = document.getElementById('er-narrative-preview');

    if (month && year && preview) {
        const diff = (parseFloat(slsuRate) - parseFloat(natRate)).toFixed(2);
        const absDiff = Math.abs(diff);
        const dir = diff >= 0 ? 'higher' : 'lower';
        const slsuP = parseInt(document.getElementById('er-slsu-passers').value) || 0;
        const slsuE = parseInt(document.getElementById('er-slsu-examinees').value) || 0;
        const ftP = parseInt(document.getElementById('er-ft-passers').value) || 0;
        const ftE = parseInt(document.getElementById('er-ft-examinees').value) || 0;
        const repP = parseInt(document.getElementById('er-rep-passers').value) || 0;
        const repE = parseInt(document.getElementById('er-rep-examinees').value) || 0;
        const natP = parseInt(document.getElementById('er-nat-passers').value) || 0;
        const natE = parseInt(document.getElementById('er-nat-examinees').value) || 0;

        preview.textContent =
            `For the ${month} ${year} Registered Mechanical Engineering Licensure Examination ` +
            `(RMeLE), Southern Luzon State University (SLSU) recorded an overall passing rate of ` +
            `${slsuRate}%, with ${slsuP} passers out of ${slsuE} examinees. The first-time takers ` +
            `achieved a passing rate of ${ftRate}% (${ftP} out of ${ftE}), while the repeaters ` +
            `obtained a passing rate of ${repRate}% (${repP} out of ${repE}). In comparison, the ` +
            `national passing rate was ${natRate}%, with ${natP.toLocaleString()} passers out of ` +
            `${natE.toLocaleString()} examinees, indicating that SLSU's overall performance was ` +
            `${absDiff} percentage points ${dir} than the national average.`;
    }
}

// ── SAVE (ADD OR UPDATE) ──────────────────────────────────────
async function saveExamResult() {
    const month = document.getElementById('er-month').value;
    const year = parseInt(document.getElementById('er-year').value);

    if (!month || !year) { toast('Month and year are required.', 'error'); return; }

    const dataSource = document.querySelector('input[name="datasource"]:checked')?.value || 'manual';

    const payload = {
        month,
        year,
        dataSource,
        slsuExaminees: parseInt(document.getElementById('er-slsu-examinees').value) || 0,
        slsuPassers: parseInt(document.getElementById('er-slsu-passers').value) || 0,
        firstTimeExaminees: parseInt(document.getElementById('er-ft-examinees').value) || 0,
        firstTimePassers: parseInt(document.getElementById('er-ft-passers').value) || 0,
        repeaterExaminees: parseInt(document.getElementById('er-rep-examinees').value) || 0,
        repeaterPassers: parseInt(document.getElementById('er-rep-passers').value) || 0,
        nationalExaminees: parseInt(document.getElementById('er-nat-examinees').value) || 0,
        nationalPassers: parseInt(document.getElementById('er-nat-passers').value) || 0,
        isPublished: document.getElementById('er-published').checked
    };

    const res = editingExamId === null
        ? await apiFetch('/examresults', { method: 'POST', body: JSON.stringify(payload) })
        : await apiFetch(`/examresults/${editingExamId}`, { method: 'PUT', body: JSON.stringify(payload) });

    if (res.ok) {
        toast(editingExamId ? 'Exam result updated!' : 'Exam result created!', 'success');
        closeModal('modal-examresult');
        await loadExamResults();
    } else {
        toast(res.data?.message || 'Failed to save.', 'error');
    }
}

// ── TOGGLE PUBLISH ────────────────────────────────────────────
async function togglePublishExamResult(id) {
    const res = await apiFetch(`/examresults/${id}/toggle-publish`, { method: 'PATCH' });
    if (res.ok) {
        toast('Publish status updated!', 'success');
        await loadExamResults();
    } else toast('Failed to update.', 'error');
}

// ── DELETE ────────────────────────────────────────────────────
function confirmDeleteExamResult(id, label) {
    document.getElementById('confirm-msg').textContent =
        `Delete exam result "${label}"? This cannot be undone.`;
    document.getElementById('confirm-ok-btn').onclick = async () => {
        const res = await apiFetch(`/examresults/${id}`, { method: 'DELETE' });
        closeModal('modal-confirm');
        if (res.ok) { toast('Deleted.', 'success'); await loadExamResults(); }
        else toast('Delete failed.', 'error');
    };
    openModal('modal-confirm');
}

// ── CLEAR FORM ────────────────────────────────────────────────
function clearExamResultForm() {
    document.getElementById('ds-manual').checked = true;
    document.getElementById('system-pull-controls').style.display = 'none';
    ['er-month', 'er-year',
        'er-slsu-examinees', 'er-slsu-passers', 'er-slsu-rate',
        'er-ft-examinees', 'er-ft-passers', 'er-ft-rate',
        'er-rep-examinees', 'er-rep-passers', 'er-rep-rate',
        'er-nat-examinees', 'er-nat-passers', 'er-nat-rate',
        'pull-month', 'pull-year'
    ].forEach(id => { const el = document.getElementById(id); if (el) el.value = ''; });
    const cb = document.getElementById('er-published');
    if (cb) cb.checked = false;
    const preview = document.getElementById('er-narrative-preview');
    if (preview) preview.textContent = 'Fill in the fields above to see the narrative preview.';
}

// ═══════════════════════════════════════════════════════════════════════════
//  INIT
// ═══════════════════════════════════════════════════════════════════════════
(async () => {
    // Clear any old token stored under the old key name before starting
    localStorage.removeItem('adminToken');

    const valid = await verifyToken();
    if (valid) await bootApp();
})();