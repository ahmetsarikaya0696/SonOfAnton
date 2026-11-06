import { chromium } from 'playwright';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';

// Only the isolated Compose development realm is used. Never point this at production.
const realm = JSON.parse(await readFile('keycloak/sonofanton-realm.json', 'utf8'));
const user = realm.users[0];
await mkdir('test-results/browser', { recursive: true });
const browser = await chromium.launch();
const page = await browser.newPage({ ignoreHTTPSErrors: true });
page.setDefaultTimeout(60000);
const errors = [];
const apiFailures = [];
page.on('response', async response => {
    if (response.url().startsWith('https://localhost:5002/') && response.status() >= 400) {
        apiFailures.push({ path: new URL(response.url()).pathname, status: response.status(),
            hasBearer: Boolean(await response.request().headerValue('authorization')),
            challenge: response.headers()['www-authenticate'] });
    }
});
page.on('pageerror', error => errors.push(error.message));
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
    await page.locator('.palette-node').filter({ hasText: 'Manuel' }).first().click();
    await page.locator('.workflow-name-input').fill('Browser acceptance');
    const saved = page.waitForResponse(r => r.url().endsWith('/api/workflows') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Kaydet', exact: true }).click();
    const saveResponse = await saved;
    assert.equal(saveResponse.status(), 200);
    assert.equal((await saveResponse.json()).graph.nodes.length, 1, 'Palette click must persist one node');
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
    await page.screenshot({ path: 'test-results/browser/completed.png', fullPage: true });
    assert.deepEqual(errors, [], 'Unhandled browser errors');
    assert.deepEqual(apiFailures, [], 'Core UI must not issue failing API requests');
    await page.locator('.log-console .log-entry').first().waitFor();
    await writeFile('test-results/browser/result.json', JSON.stringify({ success: true, executions: 4 }));
} catch (error) {
    console.error('API diagnostics:', JSON.stringify(apiFailures));
    await page.screenshot({ path: 'test-results/browser/failure.png', fullPage: true });
    await writeFile('test-results/browser/failure.txt', `${error.message}\n${errors.join('\n')}\n${await page.locator('body').innerText()}`);
    throw error;
} finally {
    await browser.close();
}
