// Opdracht 3 - cryptografie. Versleuteling gebeurt client-side.
const ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
const MOD = ALPHABET.length; // 37

// In het codeboek staat "_" voor een spatie -> omzetten voor we de sleutel gebruiken.
const fixKey = k => k.replace(/_/g, ' ');

export function normalize(text) {
    return [...text.toUpperCase()].filter(c => ALPHABET.includes(c)).join('');
}
export function encrypt(message, key) {           // C = (M + K) mod 37
    const m = normalize(message); key = fixKey(key);
    if (m.length > key.length) throw new Error(`Bericht (${m.length}) langer dan sleutel (${key.length}).`);
    let out = '';
    for (let i = 0; i < m.length; i++)
        out += ALPHABET[(ALPHABET.indexOf(m[i]) + ALPHABET.indexOf(key[i])) % MOD];
    return out;
}
export function decrypt(ciphertext, key) {        // M = (C - K) mod 37
    key = fixKey(key); let out = '';
    for (let i = 0; i < ciphertext.length; i++)
        out += ALPHABET[(ALPHABET.indexOf(ciphertext[i]) - ALPHABET.indexOf(key[i]) + MOD) % MOD];
    return out;
}

const enc = new TextEncoder();
const importMacKey = k => crypto.subtle.importKey('raw', enc.encode(k),
    { name: 'HMAC', hash: 'SHA-256' }, false, ['sign', 'verify']);
const toSign = (line, c) => enc.encode(line + ':' + c);
const toHex  = b => [...new Uint8Array(b)].map(x => x.toString(16).padStart(2,'0')).join('');
const fromHex = h => Uint8Array.from(h.match(/../g).map(x => parseInt(x,16)));

export async function computeMac(k, line, c) {
    return toHex(await crypto.subtle.sign('HMAC', await importMacKey(k), toSign(line, c)));
}
export async function verifyMac(k, line, c, macHex) {
    return crypto.subtle.verify('HMAC', await importMacKey(k), fromHex(macHex), toSign(line, c));
}
