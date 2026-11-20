// Praat met de API. De identiteit komt uit het access token, niet uit een header
// die je zelf kan invullen.

import { accessToken, currentUser, login, logout } from './auth.js';

export { currentUser, login, logout };

async function call(method, path, body) {
    const token = await accessToken();
    if (token === null) {
        return null;   // we worden doorgestuurd naar de identity provider
    }

    const headers = { 'Authorization': 'Bearer ' + token };
    const options = { method, headers };

    if (body !== undefined) {
        headers['Content-Type'] = 'application/json';
        options.body = JSON.stringify(body);
    }

    const response = await fetch('/api' + path, options);

    if (response.status === 401) {
        await login();
        return null;
    }
    if (response.status === 403) {
        throw new Error('Geen toegang. Die beslissing komt van de autorisatieserver.');
    }
    if (!response.ok) {
        throw new Error(`${method} ${path} gaf ${response.status}`);
    }

    const type = response.headers.get('Content-Type') || '';
    return type.includes('application/json') ? response.json() : response.text();
}

export const api = {
    me: () => call('GET', '/me'),
    users: () => call('GET', '/users'),
    inbox: (from) => call('GET', '/messages' + (from ? '?from=' + encodeURIComponent(from) : '')),
    search: (q) => call('GET', '/messages/search?q=' + encodeURIComponent(q)),
    message: (id) => call('GET', '/messages/' + id),
    send: (message) => call('POST', '/messages', message),
    remove: (id) => call('DELETE', '/messages/' + id),
    exportJson: () => call('GET', '/messages/export'),
};
