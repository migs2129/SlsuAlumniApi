
const API = '/api';

// ── STATE ─────────────────────────────────────────────────────
let allAlumni = [];
let rmeData = null;
let batchFilter = '';
let examResultsData = [];
let topNotchers = [];


// ── UTILITIES ─────────────────────────────────────────────────
async function apiFetch(path) {
    try {
        const res = await fetch(API + path);
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        return await res.json();
    } catch (err) {
        console.error(`[API] Failed: ${path}`, err);
        return null;
    }
}

function showLoading(id, msg = 'Loading data...') {
    const el = document.getElementById(id);
    if (el) el.innerHTML = `
        <div class="load-state">
            <div class="spinner"></div>
            <p>${msg}</p>
        </div>`;
}

function showError(id, msg = 'Could not load data.') {
    const el = document.getElementById(id);
    if (el) el.innerHTML = `
        <div class="load-state">
            <p style="color:#dc2626;">
                <i class="fa-solid fa-triangle-exclamation" style="margin-right:6px;"></i>${msg}
            </p>
        </div>`;
}

function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value;
}

function openModal(id) {
    document.getElementById(id).classList.add('open');
    document.body.style.overflow = 'hidden';
}
function closeModal(id) {
    document.getElementById(id).classList.remove('open');
    document.body.style.overflow = '';
}

// Close modals on backdrop click
document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', e => {
        if (e.target === overlay) closeModal(overlay.id);
    });
});

// Close on Escape
document.addEventListener('keydown', e => {
    if (e.key === 'Escape')
        document.querySelectorAll('.modal-overlay.open').forEach(o => closeModal(o.id));
});

// Scroll animations
const fadeObserver = new IntersectionObserver(entries => {
    entries.forEach(entry => {
        if (entry.isIntersecting) entry.target.classList.add('show');
    });
}, { threshold: 0.1 });

document.querySelectorAll('section, .hero-card, .hstat, .about-card').forEach(el => {
    el.classList.add('fade-in');
    fadeObserver.observe(el);
});

// ── 1. SUMMARY STATS ──────────────────────────────────────────
async function loadSummary() {
    const data = await apiFetch('/alumni/analytics/summary');
    if (!data) return;
    setText('stat-total', data.totalAlumni ?? '—');
    setText('stat-employed', data.totalEmployed ?? '—');
    setText('stat-licensed', data.totalRmePassers ?? '—');
    setText('emp-total', data.totalAlumni ?? '—');
}

// ── 2. ALL ALUMNI ─────────────────────────────────────────────
async function loadAlumni() {
    showLoading('batch-boxes');
    showLoading('field-list');
    const data = await apiFetch('/alumni');
    if (!data) { showError('batch-boxes'); showError('field-list'); return; }
    allAlumni = data;
    renderBatchBoxes();
    buildFieldList();
}

// ── 3. EXAM RESULTS (board exam) ──────────────────────────────
async function loadRme() {
    showLoading('rme-year-boxes');
    const data = await apiFetch('/examresults');
    if (!data || !data.length) {
        setText('rme-overall', 'N/A');
        setText('rme-banner-sub', 'No published exam results yet.');
        showError('rme-year-boxes', 'No published board exam results found.');
        return;
    }
    examResultsData = data;

    const totalPassers = data.reduce((s, e) => s + e.slsuPassers, 0);
    const totalExaminees = data.reduce((s, e) => s + e.slsuExaminees, 0);
    const overallRate = totalExaminees > 0
        ? (totalPassers / totalExaminees * 100).toFixed(2) : 0;
    setText('rme-overall', `${overallRate}%`);
    setText('rme-banner-sub', `${totalPassers} passers out of ${totalExaminees} examinees across all published periods`);
    setText('emp-pass-rate', `${overallRate}%`);
    buildExamResultBoxes();
}

// ── BATCH SEARCH ──────────────────────────────────────────────
function onBatchSearch(value) {
    batchFilter = value.trim();
    const clearBtn = document.getElementById('batch-clear-btn');
    if (clearBtn) clearBtn.style.display = batchFilter ? 'inline-block' : 'none';
    renderBatchBoxes();
}

function clearBatchSearch() {
    batchFilter = '';
    const input = document.getElementById('batch-search-input');
    const clearBtn = document.getElementById('batch-clear-btn');
    if (input) input.value = '';
    if (clearBtn) clearBtn.style.display = 'none';
    renderBatchBoxes();
}

// ── BATCH BOXES ───────────────────────────────────────────────
function renderBatchBoxes() {
    const container = document.getElementById('batch-boxes');
    if (!container) return;

    const allYears = [...new Set(
        allAlumni.map(a => a.yearGraduated).filter(Boolean)
    )].sort((a, b) => b - a);

    setText('emp-batches', allYears.length);

    const filtered = batchFilter
        ? allYears.filter(yr => yr.includes(batchFilter))
        : allYears;

    const label = document.getElementById('batch-count-label');
    if (label) {
        if (batchFilter) {
            label.textContent = filtered.length === 0
                ? 'No match'
                : `${filtered.length} of ${allYears.length} batch${allYears.length !== 1 ? 'es' : ''}`;
            label.style.color = filtered.length === 0 ? '#dc2626' : 'var(--gray-text)';
        } else {
            label.textContent = `${allYears.length} batch${allYears.length !== 1 ? 'es' : ''}`;
            label.style.color = 'var(--gray-text)';
        }
    }

    if (!filtered.length) {
        container.innerHTML = `
            <div style="grid-column:1/-1;text-align:center;padding:3rem 2rem;
                        background:white;border-radius:12px;border:1px solid var(--gray-2);color:var(--gray-text);">
                <i class="fa-solid fa-calendar-xmark" style="font-size:2rem;display:block;margin-bottom:12px;color:var(--gray-3);"></i>
                No batch found for <strong>"${batchFilter}"</strong>
            </div>`;
        return;
    }

    container.innerHTML = filtered.map(yr => {
        const count = allAlumni.filter(a => a.yearGraduated === yr).length;
        return `
            <div class="batch-box" onclick="openAlumniBatchModal('${yr}')">
                <div class="bb-icon"><i class="fa-solid fa-users"></i></div>
                <div class="bb-label">Alumni Batch</div>
                <div class="bb-year">${yr}</div>
                <div class="bb-count">${count} alumni</div>
            </div>`;
    }).join('');
}

// ── FIELD LIST ────────────────────────────────────────────────
function buildFieldList() {
    const container = document.getElementById('field-list');
    if (!container) return;

    const fieldCounts = {};
    allAlumni.forEach(a => {
        if (a.industry) fieldCounts[a.industry] = (fieldCounts[a.industry] || 0) + 1;
    });

    const sorted = Object.entries(fieldCounts).sort((a, b) => b[1] - a[1]);
    const total = allAlumni.filter(a => a.industry).length || 1;

    if (sorted[0]) setText('emp-top-field', sorted[0][0]);

    if (!sorted.length) {
        container.innerHTML = '<p style="color:var(--gray-text);">No industry data available.</p>';
        return;
    }

    const colorClasses = ['top1', 'top2', 'top3'];
    container.innerHTML = sorted.map(([name, count], i) => {
        const pct = Math.round((count / total) * 100);
        const colorCls = i < 3 ? colorClasses[i] : 'rest';
        const safeName = name.replace(/'/g, "\\'");
        return `
            <div class="field-row" onclick="openFieldModal('${safeName}')">
                <div class="field-row-top">
                    <span class="fr-name">
                        <i class="fa-solid fa-industry" style="margin-right:6px;font-size:11px;color:var(--gray-text);"></i>
                        ${name}
                    </span>
                    <span>
                        <span class="fr-count">${count} alumni</span>
                        <span class="fr-pct" style="margin-left:12px;">${pct}%</span>
                    </span>
                </div>
                <div class="gauge-track">
                    <div class="gauge-fill ${colorCls}" style="width:${pct}%"></div>
                </div>
            </div>`;
    }).join('');
}

// ── EXAM RESULT BOXES ─────────────────────────────────────────
function buildExamResultBoxes() {
    const container = document.getElementById('rme-year-boxes');
    if (!container) return;

    if (!examResultsData.length) {
        container.innerHTML = `<p style="color:var(--gray-text);text-align:center;grid-column:1/-1;padding:2rem;">No published results available.</p>`;
        return;
    }

    container.innerHTML = examResultsData.map(e => {
        const colorCls = e.slsuPassingRate >= 75 ? 'passed'
            : e.slsuPassingRate >= 50 ? 'mid' : 'low';
        return `
            <div class="year-box ${colorCls}" onclick="openExamResultModal(${e.id})">
                <div style="font-size:13px;font-weight:700;margin-bottom:4px;line-height:1.2;">
                    ${e.month} ${e.year}
                </div>
                <div class="yb-rate">${e.slsuPassingRate}%</div>
                <div class="yb-count">${e.slsuPassers}/${e.slsuExaminees} passed</div>
            </div>`;
    }).join('');
}

// ── ALUMNI BATCH MODAL ────────────────────────────────────────
// Shows scrollable list of name + job title
// Clicking a name opens the alumni profile modal
function openAlumniBatchModal(year) {
    const list = allAlumni.filter(a => a.yearGraduated === year);

    setText('alumni-modal-title',
        `Alumni Batch ${year} — ${list.length} alumni`);

    const listContainer = document.getElementById('alumni-modal-tbody');
    const noData = document.getElementById('alumni-no-data');
    if (!list.length) {
        listContainer.innerHTML = `
        <tr>
            <td colspan="7" style="text-align:center;color:var(--gray-text);padding:20px;">
                No alumni records found.
            </td>
        </tr>`;
    } else {
        listContainer.innerHTML = list.map((a, i) => `
        <tr onclick="openAlumniProfile(${i}, '${year}')" style="cursor:pointer;">
            <td>${i + 1}</td>
            <td style="font-weight:600;">${a.fullName || '—'}</td>
            <td>${a.jobTitle || '—'}</td>
            <td>${a.companyName || '—'}</td>
            <td>${a.industry || '—'}</td>
            <td>${a.employmentType || '—'}</td>
            <td>${a.passedLicensureExam === 'Yes' ? 'Passed' : '—'}</td>
        </tr>
    `).join('');
    }

    closeSearchDropdown();
    openModal('alumni-modal');
}
async function loadTopNotchers(year, month) {
    const container = document.getElementById('rme-top-notchers-list');
    if (!container) return;

    container.innerHTML = `<p style="color:var(--gray-text); font-size:13px;">Loading...</p>`;

    const data = await apiFetch(
        `/alumni/analytics/top-notchers?year=${year}&month=${month}`
    );

    let topNotchers = [];

    try {
        topNotchers = Array.isArray(data)
            ? data
            : JSON.parse(data || "[]");
    } catch (e) {
        topNotchers = [];
    }

    if (!topNotchers.length) {
        container.innerHTML = `
            <p style="color:var(--gray-text); font-size:13px;">
                No top notcher data available for this exam period.
            </p>`;
        return;
    }

    container.innerHTML = topNotchers.map(a => `
        <div class="notcher-card">
            <div class="notcher-rank">
                <i class="fa-solid fa-trophy"></i>
                ${a.awards}
            </div>

            <div class="notcher-body">
                <div class="notcher-name-box">
                    ${a.fullName}
                </div>

                <div class="notcher-sub">
                    ${a.passerStatus} · ${a.companyName || '—'}
                </div>
            </div>
        </div>
    `).join('');
}
// ── ALUMNI PROFILE MODAL ──────────────────────────────────────
function openAlumniProfile(index, year) {
    const list = allAlumni.filter(a => a.yearGraduated === year);
    const a = list[index];
    if (!a) return;

    // Name & avatar
    setText('profile-name', a.fullName || '—');
    setText('profile-avatar', (a.fullName || '?')[0].toUpperCase());
    setText('profile-batch', `Batch ${a.yearGraduated || '—'}`);

    // Personal
    setText('profile-email', a.email || a.emailAddress || '—');
    setText('profile-contact', a.contactNumber || '—');
    setText('profile-address', a.presentAddress || '—');

    // Academic
    setText('profile-enrolled', a.yearEnrolled || '—');
    setText('profile-graduated', a.yearGraduated || '—');
    setText('profile-gradprog', a.graduateSchoolProgram || 'N/A');

    // Career
    setText('profile-jobtitle', a.jobTitle || '—');
    setText('profile-company', a.companyName || '—');
    setText('profile-industry', a.industry || '—');
    setText('profile-emptype', a.employmentType || '—');
    setText('profile-location', a.jobLocation || '—');

    // Licensure
    const passed = a.passedLicensureExam === 'Yes';
    const rmeEl = document.getElementById('profile-rme-status');
    if (rmeEl) {
        rmeEl.textContent = passed ? 'Passed' : 'Did Not Pass';
        rmeEl.style.color = passed ? '#16a34a' : '#dc2626';
    }
    setText('profile-rme-month', a.monthTaken || '—');
    setText('profile-rme-year', a.yearTaken || '—');
    setText('profile-passer-status', a.passerStatus || '—');

    // Awards — show N/A if empty
    const awardsEl = document.getElementById('profile-awards');
    if (awardsEl) {
        const hasAward = a.awards && a.awards.trim() !== '';
        awardsEl.textContent = hasAward ? a.awards : 'N/A';
        awardsEl.style.color = hasAward ? '#92400e' : 'var(--gray-text)';
        awardsEl.style.background = hasAward ? '#fef9c3' : 'transparent';
        awardsEl.style.padding = hasAward ? '3px 10px' : '0';
        awardsEl.style.borderRadius = hasAward ? '20px' : '0';
    }

    openModal('alumni-profile-modal');
}

// ── EXAM RESULT MODAL ─────────────────────────────────────────
function openExamResultModal(id) {
    const e = examResultsData.find(r => Number(r.id) === Number(id));
    if (!e) return;

    setText('rme-modal-title', `MELE — ${e.month} ${e.year}`);
    setText('rme-m-takers', e.slsuExaminees);
    setText('rme-m-passers', e.slsuPassers);
    setText('rme-m-failed', e.slsuExaminees - e.slsuPassers);
    setText('rme-m-rate', `${e.slsuPassingRate}%`);

    const rateEl = document.getElementById('rme-m-rate');
    if (rateEl) {
        rateEl.style.color = e.slsuPassingRate >= 75 ? '#16a34a'
            : e.slsuPassingRate >= 50 ? '#f59e0b' : '#dc2626';
    }

    setText('rme-m-ft-rate', `${e.firstTimePassingRate}%`);
    setText('rme-m-ft-detail', `${e.firstTimePassers} / ${e.firstTimeExaminees}`);
    setText('rme-m-rep-rate', `${e.repeaterPassingRate}%`);
    setText('rme-m-rep-detail', `${e.repeaterPassers} / ${e.repeaterExaminees}`);
    setText('rme-m-nat-rate', `${e.nationalPassingRate}%`);
    setText('rme-m-nat-detail', `${(e.nationalPassers || 0).toLocaleString()} / ${(e.nationalExaminees || 0).toLocaleString()}`);

    // Charts
    const chartsSection = document.getElementById('rme-modal-charts');
    if (chartsSection) chartsSection.style.display = 'block';

    if (window._rmeModalCharts) {
        window._rmeModalCharts.forEach(c => { try { c.destroy(); } catch (_) { } });
    }
    window._rmeModalCharts = [];

    const barCtx = document.getElementById('rme-modal-bar')?.getContext('2d');
    if (barCtx) {
        window._rmeModalCharts.push(new Chart(barCtx, {
            type: 'bar',
            data: {
                labels: ['First Time', 'Repeaters', 'SLSU Overall', 'National'],
                datasets: [{
                    data: [e.firstTimePassingRate, e.repeaterPassingRate,
                    e.slsuPassingRate, e.nationalPassingRate],
                    backgroundColor: ['#2563EB', '#f59e0b', '#183356', '#16a34a'],
                    borderRadius: 6
                }]
            },
            options: {
                responsive: true,
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true, max: 100, ticks: { callback: v => v + '%' } } }
            }
        }));
    }

    const doughnutCtx = document.getElementById('rme-modal-doughnut')?.getContext('2d');
    if (doughnutCtx && (e.firstTimeExaminees + e.repeaterExaminees) > 0) {
        window._rmeModalCharts.push(new Chart(doughnutCtx, {
            type: 'doughnut',
            data: {
                labels: ['First Time Takers', 'Repeaters'],
                datasets: [{
                    data: [e.firstTimeExaminees, e.repeaterExaminees],
                    backgroundColor: ['#2563EB', '#f59e0b'],
                    borderWidth: 2
                }]
            },
            options: { responsive: true, plugins: { legend: { position: 'bottom' } } }
        }));
    }

    // Narrative
    const narrativeEl = document.getElementById('rme-modal-narrative');
    if (narrativeEl) narrativeEl.textContent = e.narrative || '—';

    // Top Passers — from ExamResult.topPassers (admin-entered)
    const topPassersList = document.getElementById('rme-top-notchers-list');

    if (topPassersList) {

        let passers = [];

        try {
            passers = JSON.parse(e.topNotchers || "[]");
        } catch (err) {
            console.error("Invalid topNotchers JSON:", err);
            passers = [];
        }

        if (!Array.isArray(passers) || passers.length === 0) {
            topPassersList.innerHTML = `
            <p style="color:var(--gray-text);font-size:13px;font-style:italic;">
                No top passers recorded for this examination period.
            </p>`;
        } else {
            topPassersList.innerHTML = passers.map((p, i) => `
            <div style="
                display:flex;align-items:center;gap:12px;
                padding:10px 14px;margin-bottom:6px;
                border-radius:8px;background:var(--gray-bg);
                border:1px solid var(--gray-2);
            ">
                <div style="
    width:32px;height:32px;
    border-radius:50%;
    background:#fef9c3;
    border:2px solid #f59e0b;
    display:flex;
    align-items:center;
    justify-content:center;
    flex-shrink:0;
    font-weight:700;
    font-size:10px;
    color:#92400e;
    text-align:center;
    overflow:hidden;
">
    ${p.rank || (i + 1)}
</div>

                <div style="flex:1;">
                    <div style="font-weight:600;font-size:13px;">${p.name}</div>
                    
                </div>

                <i class="fa-solid fa-trophy" style="color:#f59e0b;font-size:14px;"></i>
            </div>
        `).join('');
        }
    }
    // IMPORTANT: use correct field names
    // AFTER you get 'e'
    openModal('rme-modal');
}

// ── FIELD MODAL ───────────────────────────────────────────────
function openFieldModal(industry) {
    const list = allAlumni.filter(a => a.industry === industry);
    setText('field-modal-title', `${industry} — ${list.length} alumni`);
    document.getElementById('field-modal-tbody').innerHTML = list.map((a, i) => `
        <tr>
            <td style="color:var(--gray-text);">${i + 1}</td>
            <td style="font-weight:600;">${a.fullName || '—'}</td>
            <td>${a.jobTitle || '—'}</td>
            <td>${a.companyName || '—'}</td>
            <td>${a.yearGraduated || '—'}</td>
            <td>${a.employmentType || '—'}</td>
        </tr>`).join('');
    openModal('field-modal');
}

// ── ALUMNI SEARCH ─────────────────────────────────────────────
const searchInput = document.getElementById('searchAlumni');
const searchDropdown = document.getElementById('search-dropdown');

function closeSearchDropdown() {
    if (searchDropdown) searchDropdown.classList.remove('open');
    if (searchInput) searchInput.value = '';
}

if (searchInput && searchDropdown) {
    searchInput.addEventListener('input', () => {
        const kw = searchInput.value.trim().toLowerCase();
        if (!kw || kw.length < 2) { searchDropdown.classList.remove('open'); return; }

        const matches = allAlumni
            .filter(a => (a.fullName || '').toLowerCase().includes(kw))
            .slice(0, 8);

        searchDropdown.innerHTML = matches.length
            ? matches.map(a => `
                <div class="search-item" onclick="openAlumniBatchModal('${a.yearGraduated}')">
                    <div class="si-name">${highlight(a.fullName, kw)}</div>
                    <div class="si-sub">
                        ${a.yearGraduated || '—'} &nbsp;·&nbsp;
                        ${a.jobTitle || '—'} &nbsp;·&nbsp;
                        ${a.companyName || '—'}
                    </div>
                </div>`).join('')
            : '<div class="search-no">No alumni found</div>';

        searchDropdown.classList.add('open');
    });

    document.addEventListener('click', e => {
        if (!e.target.closest('.search-wrap')) searchDropdown.classList.remove('open');
    });

    searchInput.addEventListener('keydown', e => {
        if (e.key === 'Escape') searchDropdown.classList.remove('open');
    });
}

function highlight(text, kw) {
    if (!text) return '—';
    const idx = text.toLowerCase().indexOf(kw);
    if (idx === -1) return text;
    return text.slice(0, idx) +
        `<mark style="background:var(--yellow);border-radius:2px;padding:0 1px;">
            ${text.slice(idx, idx + kw.length)}
         </mark>` +
        text.slice(idx + kw.length);
}

// ── INIT ──────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadSummary();
    loadAlumni();
    loadRme();
});