'use strict';
const $ = id => document.getElementById(id);
let credentials = null, sessionVersion = 0, session = null, page = 1, total = 0;
let report = null, proposal = null, transcriptId = null, templates = [], equipment = [], signKey = null;
let clientConfig = { developmentAuthentication: false };
const formData = id => Object.fromEntries(new FormData($(id)));
function message(text, error = false) { $('message').textContent = text; $('message').classList.toggle('error', error); }
function task(fn) { return async event => { event?.preventDefault(); try { await fn(event); } catch (error) { message(error.message, true); } }; }
function tab(name) { document.querySelectorAll('.tab').forEach(x => x.hidden = x.id !== name); document.querySelectorAll('[data-tab]').forEach(x => x.classList.toggle('active', x.dataset.tab === name)); }
function facilityPath(path) { if (!$('facility').value) throw new Error('Choose an authorized facility first.'); return `/api/v1/facilities/${$('facility').value}/${path}`; }
async function api(path, method = 'GET', body, download = false) {
  if (!credentials) throw new Error('Connect to your clinic first.');
  const stamp = sessionVersion;
  const headers = { Authorization: `Bearer ${credentials.token}` };
  if (clientConfig.developmentAuthentication) {
    headers['X-RadIntel-User'] = credentials.user;
    headers['X-RadIntel-Tenant'] = credentials.tenant;
    if (credentials.mfa) headers['X-RadIntel-Mfa'] = 'true';
  }
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body), cache: 'no-store' });
  if (stamp !== sessionVersion || !credentials) throw new Error('The session changed. Connect again before continuing.');
  if (!response.ok) { const problem = await response.json().catch(() => ({})); throw new Error(problem.detail || problem.title || `Request failed (${response.status}).`); }
  return download ? response.blob() : response.json();
}
function option(select, value, text) { const item = document.createElement('option'); item.value = value; item.textContent = text; select.append(item); }
function record(container, title, detail, actions = []) {
  const row = document.createElement('article'); row.className = 'record';
  const content = document.createElement('div'), heading = document.createElement('strong'), info = document.createElement('p');
  heading.textContent = title; info.textContent = detail; content.append(heading, info); row.append(content);
  const buttons = document.createElement('div'); buttons.className = 'actions';
  for (const [text, handler] of actions) { const button = document.createElement('button'); button.type = 'button'; button.className = 'secondary'; button.textContent = text; button.addEventListener('click', task(handler)); buttons.append(button); }
  row.append(buttons); container.append(row);
}
function empty(container, text) { container.replaceChildren(); const p = document.createElement('p'); p.textContent = text; container.append(p); }
function roles() { return session?.facilities.find(f => f.id === $('facility').value)?.roles || 0; }
function resetReport() { report = proposal = transcriptId = signKey = null; $('report-form').reset(); $('transcript-form').reset(); $('proposal-panel').hidden = true; $('version-list').replaceChildren(); updateReportButtons(); }
function updateReportButtons() {
  const signed = report?.status === 'Signed';
  $('report-status').textContent = report ? `${report.kind ? 'Addendum · ' : ''}${report.status} · revision ${report.currentVersion}` : 'No report selected';
  $('report-context').textContent = report ? `Report ${report.id}` : 'Open or create a report from an assigned study in the worklist.';
  for (const field of $('report-form').querySelectorAll('input,textarea,select')) field.disabled = !report || signed;
  $('save-report').disabled = !report || signed;
  $('submit-report').disabled = !report || report.status !== 'Draft' || !report.currentVersion;
  $('sign-report').disabled = !report || report.status !== 'PendingReview';
  $('export-report').disabled = !signed;
  $('addendum-report').disabled = !signed || report.kind !== 0;
  $('generate-proposal').disabled = !report || signed;
}
async function loadSession() {
  session = await api('/api/v1/session');
  $('identity').textContent = session.userId;
  $('connection').hidden = true; $('workspace').hidden = false;
  const previous = $('facility').value; $('facility').replaceChildren();
  session.facilities.forEach(f => option($('facility'), f.id, f.name));
  if (session.facilities.some(f => f.id === previous)) $('facility').value = previous;
  $('onboard-form').hidden = !session.canProvision;
  if (!session.facilities.length) { tab('administration'); message('No facility membership yet. An authorized provisioning identity can onboard the organization.'); return; }
  await refresh();
}
async function refresh() {
  if (!$('facility').value) return;
  const role = roles();
  if (role & 15) {
    const patients = await api(facilityPath('patients?pageSize=100'));
    $('order-patient').replaceChildren(); $('patient-list').replaceChildren();
    patients.items.forEach(p => { option($('order-patient'), p.id, `${p.displayName} · ${p.identifier}`); record($('patient-list'), p.displayName, `${p.identifierIssuer}: ${p.identifier} · ${p.birthDate || 'Birth date not recorded'}`); });
    if (!patients.items.length) empty($('patient-list'), 'No patients in your permitted scope.');
    if (patients.total > 100) message('Showing the first 100 patients. The paginated API supports the complete registry.');
    equipment = await api(facilityPath('equipment'));
    await loadOrders();
  }
  if (role & 9) { templates = await api(facilityPath('templates')); renderTemplates(); }
  $('patient-form').hidden = !(role & 3); $('order-form').hidden = !(role & 3); $('template-form').hidden = !(role & 1);
  $('membership-form').hidden = $('equipment-form').hidden = !(role & 1);
}
async function loadOrders() {
  const result = await api(facilityPath(`worklist?page=${page}&pageSize=20&status=${encodeURIComponent($('status-filter').value)}`));
  total = result.total; $('order-list').replaceChildren();
  for (const order of result.items) {
    const actions = [], role = roles();
    if ((role & 3) && ['Requested','Scheduled'].includes(order.status)) actions.push(['Check in', () => transition(order, 'CheckedIn')], ['Schedule', () => schedule(order)], ['Cancel', () => transition(order, 'Cancelled')]);
    if ((role & 4) && order.status === 'CheckedIn') actions.push(['Record acquisition', () => transition(order, 'Acquired')]);
    if ((role & 8) && order.status === 'Acquired') actions.push(['Claim study', async () => { await api(facilityPath(`imaging-orders/${order.id}/claim`), 'POST', { expectedToken: order.concurrencyToken }); await loadOrders(); }]);
    if ((role & 8) && order.assignedTo === session.userId && order.status === 'InReview') actions.push(['Create report', async () => { const created = await api(facilityPath(`imaging-orders/${order.id}/reports`), 'POST', { expectedToken: order.concurrencyToken }); await openReport(created.id); }]);
    if ((role & 8) && order.assignedTo === session.userId && ['ReportDraft','Signed'].includes(order.status)) actions.push(['Open reports', async () => {
      const reports = await api(facilityPath(`imaging-orders/${order.id}/reports`));
      if (reports.length === 1) await openReport(reports[0].id);
      else { $('version-list').replaceChildren(); tab('reporting'); reports.forEach(r => record($('version-list'), `${r.kind ? 'Addendum' : 'Original'} · ${r.status}`, r.id, [['Open', () => openReport(r.id)]])); }
    }]);
    if ((role & 4) || ((role & 8) && order.assignedTo === session.userId)) {
      if (['Acquired','InReview','ReportDraft'].includes(order.status)) actions.push(['Link PACS study', () => linkStudy(order)]);
      actions.push(['Linked studies', () => linkedStudies(order)]);
    }
    record($('order-list'), `${order.modality} · ${order.bodyRegion}`, `${order.status} · ${order.priority} · Study ${order.id}`, actions);
  }
  if (!result.items.length) empty($('order-list'), 'No studies match your permitted worklist.');
  $('page-label').textContent = `Page ${page} · ${total} studies`; $('previous-page').disabled = page <= 1; $('next-page').disabled = page * 20 >= total;
}
async function transition(order, status) {
  const reason = prompt(`Reason for ${status}:`); if (!reason?.trim()) return;
  await api(facilityPath(`imaging-orders/${order.id}/transition`), 'POST', { expectedToken: order.concurrencyToken, status, reason }); await loadOrders();
}
async function schedule(order) {
  if (!equipment.length) throw new Error('Add equipment in Administration first.');
  const chosen = prompt(`Equipment ID:\n${equipment.map(x => `${x.name} (${x.modality}): ${x.id}`).join('\n')}`);
  if (!chosen) return;
  const starts = prompt('Appointment start with time zone, for example 2026-10-20T09:00:00+02:00:'); if (!starts) return;
  const ends = prompt('Appointment end with time zone:'); if (!ends) return;
  await api(facilityPath(`imaging-orders/${order.id}/schedule`), 'POST', { expectedToken: order.concurrencyToken, equipmentId: chosen, startsAt: starts, endsAt: ends }); await loadOrders();
}
async function linkStudy(order) {
  const archives = await api(facilityPath('archives'));
  if (!archives.length) throw new Error('No PACS archive is configured for this facility. Manual reporting remains available.');
  const archiveKey = prompt(`Choose archive key:\n${archives.map(a => `${a.name}: ${a.key}`).join('\n')}`);
  if (!archiveKey) return;
  const studyInstanceUid = prompt('Study Instance UID from the completed acquisition:'); if (!studyInstanceUid) return;
  await api(facilityPath(`imaging-orders/${order.id}/studies`), 'POST', { expectedToken: order.concurrencyToken, archiveKey, studyInstanceUid });
  await loadOrders(); message('Study linked after PACS patient identifier and issuer verification.');
}
async function linkedStudies(order) {
  const studies = await api(facilityPath(`imaging-orders/${order.id}/studies`));
  if (!studies.length) { message('No PACS studies are linked to this order.'); return; }
  const studyId = studies.length === 1 ? studies[0].id : prompt(`Choose linked study ID:\n${studies.map(s => `${s.archiveKey}: ${s.id}`).join('\n')}`);
  if (!studyId || !studies.some(s => s.id === studyId)) return;
  const metadata = await api(facilityPath(`studies/${studyId}/dicomweb/metadata`));
  const blob = new Blob([JSON.stringify(metadata, null, 2)], { type: 'application/dicom+json' });
  const url = URL.createObjectURL(blob), anchor = document.createElement('a'); anchor.href = url; anchor.download = `study-${studyId}-metadata.json`; anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000); message('Authorized study metadata downloaded. Diagnostic viewer integration is not enabled.');
}
function renderTemplates() {
  $('template-list').replaceChildren(); $('draft-template').replaceChildren();
  for (const t of templates) {
    const actions = [];
    if ((roles() & 9) === 9) actions.push([t.published ? 'Revoke' : 'Publish', async () => { await api(facilityPath(`templates/${t.id}/${t.published ? 'revoke' : 'publish'}`), 'POST', { expectedToken: t.concurrencyToken }); await refresh(); }]);
    record($('template-list'), `${t.name} · v${t.version} · ${t.language}`, `${t.modality} · ${t.bodyRegion} · ${t.published ? 'Published' : 'Unpublished'}`, actions);
    if (t.published) option($('draft-template'), t.id, `${t.name} · ${t.language} · ${t.modality} · v${t.version}`);
  }
  if (!templates.length) empty($('template-list'), 'No templates have been created.');
}
async function openReport(id) {
  const detail = await api(facilityPath(`reports/${id}`));
  report = detail.report; transcriptId = report.checkedTranscriptId; proposal = null; signKey = null;
  $('proposal-panel').hidden = true;
  const latest = detail.versions.at(-1);
  for (const name of ['examination','findings','impression','language']) $('report-form').elements[name].value = latest?.[name] || (name === 'language' ? 'en' : '');
  $('version-list').replaceChildren();
  detail.versions.forEach(v => record($('version-list'), `Revision ${v.number}`, `${new Date(v.createdAt).toLocaleString()} · ${v.authorId}`));
  detail.addenda.forEach(a => record($('version-list'), `Addendum · ${a.status}`, a.amendmentReason, [['Open addendum', () => openReport(a.id)]]));
  updateReportButtons(); tab('reporting');
}
document.querySelectorAll('[data-tab]').forEach(x => x.addEventListener('click', () => tab(x.dataset.tab)));
$('connect-form').addEventListener('submit', task(async () => {
  credentials = { token: $('token').value, user: $('user-id').value, tenant: $('tenant-id').value, mfa: $('simulate-mfa').checked }; sessionVersion++;
  $('token').value = ''; await loadSession(); message('Connected to your authorized workspace.');
}));
$('disconnect').addEventListener('click', () => {
  credentials = session = null; sessionVersion++; templates = []; equipment = [];
  $('workspace').hidden = true; $('connection').hidden = false; $('identity').textContent = 'Disconnected';
  ['order-list','patient-list','template-list','audit-list'].forEach(id => $(id).replaceChildren());
  document.querySelectorAll('#workspace form').forEach(form => form.reset());
  ['facility','order-patient','draft-template'].forEach(id => $(id).replaceChildren());
  resetReport(); message('Disconnected.');
});
$('facility').addEventListener('change', task(async () => { sessionVersion++; resetReport(); page = 1;
  document.querySelectorAll('#workspace form').forEach(form => form.reset());
  ['order-list','patient-list','template-list','audit-list'].forEach(id => $(id).replaceChildren());
  await refresh(); }));
$('refresh').addEventListener('click', task(refresh));
$('status-filter').addEventListener('change', task(async () => { page = 1; await loadOrders(); }));
$('previous-page').addEventListener('click', task(async () => { page--; await loadOrders(); }));
$('next-page').addEventListener('click', task(async () => { page++; await loadOrders(); }));
$('onboard-form').addEventListener('submit', task(async () => { await api('/api/v1/organizations/onboard', 'POST', formData('onboard-form')); await loadSession(); message('Organization and first facility created.'); }));
$('patient-form').addEventListener('submit', task(async () => { const data = formData('patient-form'); data.birthDate ||= null; await api(facilityPath('patients'), 'POST', data); $('patient-form').reset(); await refresh(); message('Patient registered.'); }));
$('order-form').addEventListener('submit', task(async () => { await api(facilityPath('imaging-orders'), 'POST', { ...formData('order-form'), patientId: $('order-patient').value }); $('order-form').reset(); await loadOrders(); message('Imaging order created.'); }));
$('equipment-form').addEventListener('submit', task(async () => { await api(facilityPath('equipment'), 'POST', formData('equipment-form')); $('equipment-form').reset(); await refresh(); message('Equipment added.'); }));
$('membership-form').addEventListener('submit', task(async () => { const form = $('membership-form'); const selected = [...form.querySelectorAll('[name=roles]:checked')].reduce((a,x) => a | Number(x.value), 0); await api(facilityPath('memberships'), 'PUT', { userId: form.elements.userId.value, roles: selected, active: form.elements.active.checked }); message('Staff access updated.'); }));
$('template-form').addEventListener('submit', task(async () => { const data = formData('template-form'); data.version = Number(data.version); await api(facilityPath('templates'), 'POST', data); $('template-form').reset(); await refresh(); message('Unpublished template version created.'); }));
$('report-form').addEventListener('submit', task(async () => { if (!report) return; const result = await api(facilityPath(`reports/${report.id}/drafts`), 'POST', { ...formData('report-form'), expectedToken: report.concurrencyToken }); await openReport(result.id); message('Draft revision saved.'); }));
$('submit-report').addEventListener('click', task(async () => { const result = await api(facilityPath(`reports/${report.id}/submit`), 'POST', { expectedToken: report.concurrencyToken }); await openReport(result.id); message('Current revision is pending your final review.'); }));
$('sign-report').addEventListener('click', task(async () => {
  if (!confirm('I reviewed this saved version against the original observations and images. Sign and freeze this exact version?')) return;
  signKey ||= crypto.randomUUID();
  await api(facilityPath(`reports/${report.id}/sign`), 'POST', { expectedToken: report.concurrencyToken, requestKey: signKey });
  await openReport(report.id); message('Report signed. Corrections require an addendum.');
}));
$('addendum-report').addEventListener('click', task(async () => { const reason = prompt('Reason for the addendum:'); if (!reason?.trim()) return; const result = await api(facilityPath(`reports/${report.id}/addenda`), 'POST', { expectedToken: report.concurrencyToken, reason }); await openReport(result.id); message('New unsigned addendum created.'); }));
$('export-report').addEventListener('click', task(async () => { const blob = await api(facilityPath(`reports/${report.id}/export`), 'GET', undefined, true); const url = URL.createObjectURL(blob), anchor = document.createElement('a'); anchor.href = url; anchor.download = `report-${report.id}.txt`; anchor.click(); setTimeout(() => URL.revokeObjectURL(url), 1000); }));
$('transcript-form').addEventListener('submit', task(async () => { if (!report) throw new Error('Open an assigned report first.'); const data = formData('transcript-form'); const result = await api(facilityPath(`reports/${report.id}/transcripts`), 'POST', { originalText: data.originalText, checkedText: data.checkedText, language: $('report-form').elements.language.value, confirmedByRadiologist: $('transcript-form').elements.confirmed.checked }); await openReport(report.id); transcriptId = result.id; message('Checked transcript saved. Earlier proposals are now stale.'); }));
$('generate-proposal').addEventListener('click', task(async () => { if (!transcriptId) throw new Error('Save a checked transcript first.'); proposal = await api(facilityPath(`reports/${report.id}/ai-draft`), 'POST', { expectedToken: report.concurrencyToken, transcriptId, templateId: $('draft-template').value }); $('proposal-text').textContent = proposal.findings; $('proposal-provider').textContent = `Provider: ${proposal.provider} · version ${proposal.modelVersion} · awaiting your review`; $('proposal-panel').hidden = false; }));
async function reviewProposal(accept) { const form = formData('report-form'); const result = await api(facilityPath(`reports/${report.id}/proposals/${proposal.id}/review`), 'POST', { expectedToken: report.concurrencyToken, accept, examination: form.examination || 'Not accepted', impression: form.impression || 'Not accepted' }); await openReport(result.report.id); message(accept ? 'Proposal accepted into an unsigned draft.' : 'Proposal rejected.'); }
$('accept-proposal').addEventListener('click', task(async () => { if (!$('report-form').elements.examination.reportValidity() || !$('report-form').elements.impression.reportValidity()) return; await reviewProposal(true); }));
$('reject-proposal').addEventListener('click', task(() => reviewProposal(false)));
$('load-audit').addEventListener('click', task(async () => { const result = await api(facilityPath('audit?pageSize=50')); $('audit-list').replaceChildren(); result.items.forEach(e => record($('audit-list'), e.action, `${new Date(e.createdAt).toLocaleString()} · ${e.actorId} · ${e.entityId}`)); }));
fetch('/client-config', { cache: 'no-store' }).then(r => r.json()).then(config => { clientConfig = config; $('development-fields').hidden = !config.developmentAuthentication; }).catch(() => {});
updateReportButtons(); tab('worklist');
