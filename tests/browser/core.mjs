import { chromium } from 'playwright';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';

// Only the isolated Compose development realm is used. Never point this at production.
const realm = JSON.parse(await readFile('keycloak/sonofanton-realm.json', 'utf8'));
const user = realm.users[0];
await mkdir('test-results/browser', { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage({ ignoreHTTPSErrors: true, viewport: { width: 1363, height: 936 } });
page.setDefaultTimeout(60000);
const errors = [];
const apiFailures = [];
let saveCount = 0;
async function openPanel(name) {
    await page.getByTitle('Daha Fazla', { exact: true }).click();
    await page.locator('.ks-menu').getByRole('button', { name, exact: true }).click();
}
async function saveWorkflow() {
    const saved = page.waitForResponse(r => r.url().endsWith('/api/workflows') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Kaydet', exact: true }).click();
    const response = await saved;
    assert.equal(response.status(), 200);
    const workflow = await response.json();
    await page.waitForURL(`**/workflows/${workflow.id}`);
    await page.getByRole('button', { name: 'Kaydet', exact: true }).waitFor();
    return workflow;
}
page.on('response', async response => {
    if (response.url().endsWith('/api/workflows') && response.request().method() === 'POST') saveCount++;
    if (response.url().startsWith('https://localhost:5002/') && response.status() >= 400) {
        apiFailures.push({ path: new URL(response.url()).pathname, status: response.status(),
            hasBearer: Boolean(await response.request().headerValue('authorization')),
            challenge: response.headers()['www-authenticate'] });
    }
});
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => {
    if (message.type() === 'error' && message.text().includes('Unhandled exception rendering component'))
        errors.push(message.text());
});
try {
    const firstApiRequest = page.waitForRequest(r => r.url().startsWith('https://localhost:5002/') && r.headers()['authorization']);
    await page.goto('https://localhost:5001');
    await page.locator('input[name="username"]').fill(user.username);
    await page.locator('input[name="password"]').fill(user.credentials.find(c => c.type === 'password').value);
    await page.locator('input[type="submit"], button[type="submit"]').first().click();
    await page.getByRole('button', { name: 'Kaydet', exact: true }).waitFor();
    const apiRequest = await firstApiRequest;
    const bearer = await apiRequest.headerValue('authorization');
    const claims = JSON.parse(Buffer.from(bearer.split('.')[1], 'base64url').toString());
    assert.equal(typeof claims.sub, 'string', 'Access token must contain subject');
    assert.ok([claims.aud].flat().includes('sonofanton-backend'), 'Access token must target backend');
    // Opening a blank editor must not create an empty workflow after the autosave interval.
    await page.waitForTimeout(4000);
    assert.equal(saveCount, 0, 'An untouched editor must not autosave');
    await page.locator('.palette-node').filter({ hasText: 'Manuel' }).first().click();
    await page.locator('.workflow-name-input').fill('Browser acceptance');
    let savedWorkflow = await saveWorkflow();
    assert.equal(savedWorkflow.graph.nodes.length, 1, 'Palette click must persist one node');
    const savedUrl = page.url();
    await page.reload();
    await page.locator('.diagram-node').waitFor();
    assert.equal(await page.locator('.workflow-name-input').inputValue(), 'Browser acceptance');
    const afterReload = saveCount;
    await page.waitForTimeout(4000);
    assert.equal(saveCount, afterReload, 'Loading a saved workflow must not produce a revision');

    // F1: use actual pointer selection, then edit and persist the selected node.
    await page.locator('.diagram-node').click();
    await page.locator('.properties-panel.open .node-name-input').fill('Manual QA');
    await page.getByRole('button', { name: 'Node ayarlarını kapat', exact: true }).click();
    savedWorkflow = await saveWorkflow();
    assert.equal(savedWorkflow.graph.nodes[0].name, 'Manual QA');

    // F4: variable edits must survive saving and a full reload.
    await openPanel('Değişkenler');
    await page.getByRole('button', { name: 'İlk Değişkeni Ekle', exact: true }).click();
    await page.locator('.var-name-input').fill('qaProbe');
    await page.locator('.var-default-input').fill('retained');
    await page.locator('.var-default-input').press('Tab');
    await page.locator('.variables-panel.open .close-btn').click();
    savedWorkflow = await saveWorkflow();
    assert.equal(savedWorkflow.graph.variables[0].name, 'qaProbe');
    assert.equal(savedWorkflow.graph.variables[0].defaultValue, 'retained');
    await page.reload();
    await page.locator('.diagram-node').waitFor();
    await openPanel('Değişkenler');
    assert.equal(await page.locator('.var-name-input').inputValue(), 'qaProbe');
    assert.equal(await page.locator('.var-default-input').inputValue(), 'retained');
    await page.locator('.variables-panel.open .close-btn').click();

    // F2: saved workflows can also be opened from the folder sidebar.
    await page.getByRole('button', { name: 'Boş Yeni', exact: true }).click();
    await page.waitForURL('https://localhost:5001/');
    await page.getByTitle('Klasörler', { exact: true }).click();
    await page.locator('.workflow-list-item').filter({ hasText: 'Browser acceptance' }).click();
    await page.waitForURL(savedUrl);
    await page.locator('.diagram-node').waitFor();
    await page.getByTitle('Klasörler', { exact: true }).click();
    for (let run = 0; run < 2; run++) {
        const executed = page.waitForResponse(r => /\/api\/workflows\/[^/]+\/execute$/.test(r.url()) && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Çalıştır', exact: true }).click();
        const executeResponse = await executed;
        assert.equal(executeResponse.status(), 200);
        const run = await executeResponse.json();
        await page.locator('.execution-id').filter({ hasText: run.id.slice(0, 8) }).waitFor();
        await page.getByText('Başarıyla tamamlandı', { exact: true }).waitFor();
    }
    for (const control of ['İleri Sar', 'Normal Hız']) {
        const started = page.waitForResponse(r => r.url().endsWith('/test-run') && r.request().method() === 'POST');
        await page.getByRole('button', { name: 'Test Et', exact: true }).click();
        const response = await started;
        assert.equal(response.status(), 200);
        const execution = await response.json();
        const authorization = await response.request().headerValue('authorization');
        let paused = false;
        for (let attempt = 0; attempt < 150; attempt++) {
            const state = await page.request.get(`https://localhost:5002/api/executions/${execution.id}`, { headers: { Authorization: authorization } });
            assert.equal(state.status(), 200);
            if ((await state.json()).status === 5) { paused = true; break; }
            await new Promise(resolve => setTimeout(resolve, 200));
        }
        assert.equal(paused, true, 'Test execution must reach Paused');
        await page.getByRole('button', { name: control, exact: true }).click();
        await page.getByText('Başarıyla tamamlandı', { exact: true }).waitFor();
    }
    await page.locator('.execution-monitor.visible .btn-close').click();

    // F5: local YAML import validates without invoking the disabled experimental API.
    await openPanel('YAML');
    await page.locator('.yaml-panel.open').getByRole('button', { name: 'Düzenle', exact: true }).click();
    await page.locator('.yaml-editor').fill('version: [');
    await page.getByTitle("YAML'dan İçe Aktar", { exact: true }).click();
    await page.locator('.validation-errors .error-item').first().waitFor();
    assert.equal(await page.locator('.diagram-node').count(), 1, 'Invalid YAML must preserve the graph');
    await page.locator('.yaml-editor').fill(`version: '1.0'
nodes:
  - id: '11111111-1111-4111-8111-111111111111'
    type: trigger.manual
    name: Manual QA
    position: {x: 100, y: 150}
  - id: '22222222-2222-4222-8222-222222222222'
    type: transform.set
    name: Set QA
    position: {x: 420, y: 150}
    parameters:
      values: '{"message":"imported"}'
connections:
  - from: '11111111-1111-4111-8111-111111111111'
    to: '22222222-2222-4222-8222-222222222222'
variables: []
`);
    await page.getByTitle("YAML'dan İçe Aktar", { exact: true }).click();
    await page.waitForFunction(() => document.querySelectorAll('.diagram-node').length === 2);
    await page.locator('.yaml-panel.open .close-btn').click();
    await page.locator('.diagram-node').filter({ hasText: 'Set QA' }).click();
    await page.locator('.properties-panel.open .param-textarea').fill('{"message":"edited in properties"}');
    await page.locator('.properties-panel.open .param-textarea').press('Tab');
    await page.getByRole('button', { name: 'Node ayarlarını kapat', exact: true }).click();
    savedWorkflow = await saveWorkflow();
    assert.equal(savedWorkflow.graph.nodes.length, 2);
    assert.equal(savedWorkflow.graph.connections.length, 1);
    assert.equal(savedWorkflow.graph.nodes.find(n => n.type === 'transform.set').parameters.values, '{"message":"edited in properties"}');

    // F6/F9: final totals are correct even for a workflow faster than SSE attachment.
    const executed = page.waitForResponse(r => /\/api\/workflows\/[^/]+\/execute$/.test(r.url()) && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Çalıştır', exact: true }).click();
    assert.equal((await executed).status(), 200);
    await page.getByText('Başarıyla tamamlandı', { exact: true }).waitFor();
    await page.locator('.execution-monitor .progress-text').filter({ hasText: '2 / 2 node' }).waitFor();
    assert.equal(await page.locator('.node-status-item').count(), 2);
    assert.ok(Number((await page.locator('.total-duration').innerText()).replace(/[^0-9.]/g, '')) > 0);
    await page.locator('.log-console .log-entry').filter({ hasText: 'başlatıldı' }).first().waitFor();
    assert.ok(!(await page.locator('.log-console').innerText()).includes('\uFFFD'));
    await page.locator('.execution-monitor.visible .btn-close').click();

    // F7: the revision drawer and its actions must be inside the viewport.
    await openPanel('Revizyon Geçmişi');
    await page.locator('.side-panel.open .revision-item').first().waitFor();
    const bounds = await page.locator('.side-panel.open').boundingBox();
    assert.ok(bounds && bounds.x >= 0 && bounds.y >= 0 && bounds.y + bounds.height <= 937);
    await page.locator('.side-panel.open').getByRole('button', { name: 'Diff', exact: true }).first().click();
    await page.locator('.diff-modal').waitFor();
    await page.getByRole('button', { name: 'Kapat', exact: true }).click();
    await page.locator('.side-panel.open .panel-header button').click();

    // F3/F2: workspace details render and their workflow links open the saved graph.
    await page.goto(`https://localhost:5001/workspaces/${savedWorkflow.workspaceId}`);
    await page.getByRole('button', { name: 'İçerik', exact: true }).click();
    await page.locator('a.workflow-link').filter({ hasText: 'Browser acceptance' }).click();
    await page.waitForURL(savedUrl);
    await page.waitForFunction(() => document.querySelectorAll('.diagram-node').length === 2);

    // F8: use a normal mouse click so an overlapping login badge fails this test.
    await page.goto('https://localhost:5001/apikeys');
    await page.getByRole('link', { name: 'Editöre Dön', exact: true }).click();
    await page.waitForURL('https://localhost:5001/');
    await page.getByRole('button', { name: 'Kaydet', exact: true }).waitFor();
    await page.screenshot({ path: 'test-results/browser/completed.png', fullPage: true });
    assert.deepEqual(errors, [], 'Unhandled browser errors');
    assert.deepEqual(apiFailures, [], 'Core UI must not issue failing API requests');
    await writeFile('test-results/browser/result.json', JSON.stringify({ success: true, executions: 5, qaRegressions: ['F1','F2','F3','F4','F5','F6','F7','F8','F9'] }));
} catch (error) {
    console.error('API diagnostics:', JSON.stringify(apiFailures));
    await page.screenshot({ path: 'test-results/browser/failure.png', fullPage: true });
    await writeFile('test-results/browser/failure.txt', `${error.message}\n${errors.join('\n')}\n${await page.locator('body').innerText()}`);
    throw error;
} finally {
    await browser.close();
}
