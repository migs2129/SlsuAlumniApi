// ═══════════════════════════════════════════════════════════════
//  SLSU BSME Alumni Tracking System — app.js
//  Connects to ASP.NET Core Web API backend
// ═══════════════════════════════════════════════════════════════

const API = '/api'; // ← change to your actual port

// ───────────────────────────────────────────────────────────────
//  STATE
// ───────────────────────────────────────────────────────────────
let allAlumni = [];
let rmeData = null;

// ───────────────────────────────────────────────────────────────
//  UTILITIES
// ───────────────────────────────────────────────────────────────

/** Generic fetch wrapper — returns parsed JSON or null on error */
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

/** Show a loading spinner inside any container */
function showLoading(containerId) {
    const el = document.getElementById(containerId);
    if (el) el.innerHTML = `
    <div class="load-state">
      <div class="spinner"></div>
      <p>Loading data...</p>
    </div>`;
}

/** Show an error message inside any container */
function showError(containerId, message = 'Could not load data. Check your API connection.') {
    const el = document.getElementById(containerId);
    if (el) el.innerHTML = `
    <div class="load-state">
      <p style="color:#dc2626;"><i class="fa-solid fa-triangle-exclamation" style="margin-right:6px;"></i>${message}</p>
    </div>`;
}

/** Open / close modals */
function openModal(id) { document.getElementById(id).classList.add('open'); document.body.style.overflow = 'hidden'; }
function closeModal(id) { document.getElementById(id).classList.remove('open'); document.body.style.overflow = ''; }

/** Smooth-scroll to a section */
function scrollTo(selector) {
    const el = document.querySelector(selector);
    if (el) el.scrollIntoView({ behavior: 'smooth' });
}

// Close modals when clicking the backdrop
document.querySelectorAll('.modal-overlay').forEach(overlay => {
    overlay.addEventListener('click', e => {
        if (e.target === overlay) closeModal(overlay.id);
    });
});

// Close modals with Escape key
document.addEventListener('keydown', e => {
    if (e.key === 'Escape') {
        document.querySelectorAll('.modal-overlay.open').forEach(o => closeModal(o.id));
    }
});

// ───────────────────────────────────────────────────────────────
//  1. SUMMARY STATS  →  hero counters + emp panel
//     GET /api/alumni/analytics/summary
//     Returns: { totalAlumni, totalEmployed, totalRmePassers }
// ───────────────────────────────────────────────────────────────
async function loadSummary() {
    const data = await apiFetch('/alumni/analytics/summary');
    if (!data) return;

    setText('stat-total', data.totalAlumni ?? '—');
    setText('stat-employed', data.totalEmployed ?? '—');
    setText('stat-licensed', data.totalRmePassers ?? '—');
    setText('emp-total', data.totalAlumni ?? '—');
}

// ───────────────────────────────────────────────────────────────
//  2. ALL ALUMNI  →  search, batch boxes, field list
//     GET /api/alumni
//     Returns: Alumni[]
// ───────────────────────────────────────────────────────────────
async function loadAlumni() {
    showLoading('batch-boxes');
    showLoading('field-list');

    const data = await apiFetch('/alumni');

    if (!data) {
        showError('batch-boxes');
        showError('field-list');
        return;
    }

    allAlumni = data;

    buildBatchBoxes();
    buildFieldList();
}

// ───────────────────────────────────────────────────────────────
//  3. RME DATA  →  banner, year boxes, emp pass rate
//     GET /api/alumni/analytics/rme-passing-rate
//     Returns: { totalTakers, totalPassers, totalFailed,
//                overallPassingRate, byPasserStatus, byYear[] }
// ───────────────────────────────────────────────────────────────
async function loadRme() {
    showLoading('rme-year-boxes');

    const data = await apiFetch('/alumni/analytics/rme-passing-rate');

    if (!data) {
        setText('rme-overall', 'N/A');
        setText('rme-banner-sub', 'Could not load data.');
        showError('rme-year-boxes');
        return;
    }

    rmeData = data;

    // Banner
    setText('rme-overall', `${data.overallPassingRate}%`);
    setText('rme-banner-sub', `${data.totalPassers} passers out of ${data.totalTakers} takers`);

    // Employment panel pass rate
    setText('emp-pass-rate', `${data.overallPassingRate}%`);

    // Year boxes
    buildRmeYearBoxes();
}

// ───────────────────────────────────────────────────────────────
//  BUILD: BATCH BOXES
// ───────────────────────────────────────────────────────────────
function buildBatchBoxes() {
    const container = document.getElementById('batch-boxes');
    if (!container) return;

    // Get unique sorted years (newest first)
    const years = [...new Set(
        allAlumni.map(a => a.yearGraduated).filter(Boolean)
    )].sort((a, b) => b - a);

    // Fill "batches tracked" in employment panel
    setText('emp-batches', years.length);

    if (!years.length) {
        container.innerHTML = '<p style="color:var(--gray-text);text-align:center;padding:2rem;">No batch data available.</p>';
        return;
    }

    container.innerHTML = years.map(yr => {
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

// ───────────────────────────────────────────────────────────────
//  BUILD: FIELD OF PRACTICE LIST (gauge bars)
// ───────────────────────────────────────────────────────────────
function buildFieldList() {
    const container = document.getElementById('field-list');
    if (!container) return;

    // Count per industry
    const fieldCounts = {};
    allAlumni.forEach(a => {
        if (a.industry) fieldCounts[a.industry] = (fieldCounts[a.industry] || 0) + 1;
    });

    const sorted = Object.entries(fieldCounts).sort((a, b) => b[1] - a[1]);
    const totalWithIndustry = allAlumni.filter(a => a.industry).length || 1;

    // Top field name for employment panel
    if (sorted[0]) setText('emp-top-field', sorted[0][0]);

    if (!sorted.length) {
        container.innerHTML = '<p style="color:var(--gray-text);">No industry data available.</p>';
        return;
    }

    const colorClasses = ['top1', 'top2', 'top3'];

    container.innerHTML = sorted.map(([name, count], i) => {
        const pct = Math.round((count / totalWithIndustry) * 100);
        const colorCls = i < 3 ? colorClasses[i] : 'rest';
        // Escape single quotes for inline onclick
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

// ───────────────────────────────────────────────────────────────
//  BUILD: RME YEAR BOXES
// ───────────────────────────────────────────────────────────────
function buildRmeYearBoxes() {
    const container = document.getElementById('rme-year-boxes');
    if (!container || !rmeData?.byYear) return;

    if (!rmeData.byYear.length) {
        container.innerHTML = '<p style="color:var(--gray-text);text-align:center;padding:2rem;">No year-by-year data available.</p>';
        return;
    }

    container.innerHTML = rmeData.byYear.map(y => {
        // Color code by passing rate
        const colorCls = y.passingRate >= 75 ? 'passed'
            : y.passingRate >= 50 ? 'mid'
                : 'low';
        return `
      <div class="year-box ${colorCls}" onclick="openRmeModal('${y.year}')">
        <div class="yb-rate">${y.passingRate}%</div>
        <div class="yb-year">Year ${y.year}</div>
        <div class="yb-count">${y.passers}/${y.takers} passed</div>
      </div>`;
    }).join('');
}

// ───────────────────────────────────────────────────────────────
//  MODAL: ALUMNI BATCH
// ───────────────────────────────────────────────────────────────
function openAlumniBatchModal(year) {
    const list = allAlumni.filter(a => a.yearGraduated === year);

    setText('alumni-modal-title', `Alumni Batch ${year} — ${list.length} record${list.length !== 1 ? 's' : ''}`);

    const tbody = document.getElementById('alumni-modal-tbody');
    const noData = document.getElementById('alumni-no-data');

    if (!list.length) {
        tbody.innerHTML = '';
        noData.style.display = 'block';
    } else {
        noData.style.display = 'none';
        tbody.innerHTML = list.map((a, i) => {
            const rmeCls = a.passedLicensureExam === 'Yes' ? 'pass' : 'fail';
            const rmeText = a.passedLicensureExam === 'Yes' ? 'Passed' : 'Not Passed';
            return `
        <tr>
          <td style="color:var(--gray-text);">${i + 1}</td>
          <td style="font-weight:600;">${a.fullName || '—'}</td>
          <td>${a.jobTitle || '—'}</td>
          <td>${a.companyName || '—'}</td>
          <td>${a.industry || '—'}</td>
          <td>${a.employmentType || '—'}</td>
          <td><span class="badge ${rmeCls}">${rmeText}</span></td>
        </tr>`;
        }).join('');
    }

    // Close search dropdown if open
    closeSearchDropdown();
    openModal('alumni-modal');
}

// ───────────────────────────────────────────────────────────────
//  MODAL: RME YEAR DETAIL
// ───────────────────────────────────────────────────────────────
function openRmeModal(year) {
    if (!rmeData?.byYear) return;

    const yr = rmeData.byYear.find(y => String(y.year) === String(year));
    if (!yr) return;

    setText('rme-modal-title', `RME Licensure Exam — ${year}`);
    setText('rme-m-takers', yr.takers);
    setText('rme-m-passers', yr.passers);
    setText('rme-m-failed', yr.takers - yr.passers);
    setText('rme-m-rate', `${yr.passingRate}%`);

    // Color code the rate
    const rateEl = document.getElementById('rme-m-rate');
    if (rateEl) {
        rateEl.style.color = yr.passingRate >= 75 ? '#16a34a'
            : yr.passingRate >= 50 ? '#f59e0b'
                : '#dc2626';
    }

    // Passer status breakdown
    const statusWrap = document.getElementById('status-tags-wrap');
    if (statusWrap) {
        const statuses = rmeData.byPasserStatus || {};
        const entries = Object.entries(statuses);
        statusWrap.innerHTML = entries.length
            ? entries.map(([k, v]) => `<span class="stag">${k}: <strong>${v}</strong></span>`).join('')
            : '<span style="color:var(--gray-text);font-size:12px;">No breakdown available.</span>';
    }

    openModal('rme-modal');
}

// ───────────────────────────────────────────────────────────────
//  MODAL: FIELD OF PRACTICE
// ───────────────────────────────────────────────────────────────
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

// ───────────────────────────────────────────────────────────────
//  SEARCH
// ───────────────────────────────────────────────────────────────
const searchInput = document.getElementById('searchAlumni');
const searchDropdown = document.getElementById('search-dropdown');

function closeSearchDropdown() {
    if (searchDropdown) searchDropdown.classList.remove('open');
    if (searchInput) searchInput.value = '';
}

if (searchInput && searchDropdown) {
    searchInput.addEventListener('input', () => {
        const kw = searchInput.value.trim().toLowerCase();

        if (!kw || kw.length < 2) {
            searchDropdown.classList.remove('open');
            return;
        }

        const matches = allAlumni
            .filter(a => (a.fullName || '').toLowerCase().includes(kw))
            .slice(0, 8);

        if (!matches.length) {
            searchDropdown.innerHTML = '<div class="search-no">No alumni found</div>';
        } else {
            searchDropdown.innerHTML = matches.map(a => `
        <div class="search-item" onclick="openAlumniBatchModal('${a.yearGraduated}')">
          <div class="si-name">${highlight(a.fullName, kw)}</div>
          <div class="si-sub">
            ${a.yearGraduated || '—'} &nbsp;·&nbsp;
            ${a.jobTitle || '—'} &nbsp;·&nbsp;
            ${a.companyName || '—'}
          </div>
        </div>`).join('');
        }

        searchDropdown.classList.add('open');
    });

    // Close when clicking outside
    document.addEventListener('click', e => {
        if (!e.target.closest('.search-wrap')) {
            searchDropdown.classList.remove('open');
        }
    });

    // Close on Escape
    searchInput.addEventListener('keydown', e => {
        if (e.key === 'Escape') searchDropdown.classList.remove('open');
    });
}

/** Wraps matched characters in a <mark> tag for the dropdown */
function highlight(text, kw) {
    if (!text) return '—';
    const idx = text.toLowerCase().indexOf(kw);
    if (idx === -1) return text;
    return (
        text.slice(0, idx) +
        `<mark style="background:var(--yellow);border-radius:2px;padding:0 1px;">${text.slice(idx, idx + kw.length)}</mark>` +
        text.slice(idx + kw.length)
    );
}

// ───────────────────────────────────────────────────────────────
//  MOBILE NAV — active state on scroll
// ───────────────────────────────────────────────────────────────
const sections = ['main-page', 'about', 'alumni-list', 'employment', 'board-exam', 'contact'];
const mobileNav = document.querySelector('.mobile-nav');

if (mobileNav) {
    const mobileItems = mobileNav.querySelectorAll('li:not(.admin-mob)');

    window.addEventListener('scroll', () => {
        let current = '';
        sections.forEach(id => {
            const el = document.getElementById(id);
            if (el && window.scrollY >= el.offsetTop - 120) current = id;
        });

        mobileItems.forEach((li, i) => {
            li.classList.toggle('active', sections[i] === current);
        });
    }, { passive: true });
}

// ───────────────────────────────────────────────────────────────
//  HELPERS
// ───────────────────────────────────────────────────────────────
function setText(id, value) {
    const el = document.getElementById(id);
    if (el) el.textContent = value;
}

// ───────────────────────────────────────────────────────────────
//  INIT — runs on page load
// ───────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    loadSummary();   // hero counters
    loadAlumni();    // batch boxes, field list, search data
    loadRme();       // rme banner, year boxes, pass rate
});
