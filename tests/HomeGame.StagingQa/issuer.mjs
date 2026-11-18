// Only the separate staging Compose project includes this synthetic issuer.
import https from 'node:https';
import { generateKeyPairSync, sign } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';

if (process.env.LUP_HOME_STAGING_QA !== 'synthetic-only-20261118')
    throw new Error('Start with the isolated Hjem staging Compose file.');
const issuer = 'https://home-game-qa-issuer:9443';
const audience = 'lup-home-game-qa-only';
const expiresAt = Math.floor(Date.now() / 1000) + 8 * 60 * 60;
mkdirSync('/tls', { recursive: true, mode: 0o700 });
mkdirSync('/public-ca', { recursive: true });
const openssl = args => execFileSync('openssl', args, { stdio: 'ignore' });
openssl(['req', '-x509', '-newkey', 'rsa:2048', '-sha256', '-nodes', '-days', '2',
    '-subj', '/CN=LUP synthetic staging QA CA', '-keyout', '/tls/ca.key', '-out', '/tls/ca.pem']);
openssl(['req', '-newkey', 'rsa:2048', '-sha256', '-nodes', '-subj', '/CN=home-game-qa-issuer',
    '-keyout', '/tls/issuer.key', '-out', '/tls/issuer.csr']);
writeFileSync('/tls/issuer.ext', 'subjectAltName=DNS:home-game-qa-issuer\n' +
    'extendedKeyUsage=serverAuth\nkeyUsage=digitalSignature,keyEncipherment\n');
openssl(['x509', '-req', '-in', '/tls/issuer.csr', '-CA', '/tls/ca.pem', '-CAkey', '/tls/ca.key',
    '-CAcreateserial', '-out', '/tls/issuer.pem', '-days', '2', '-sha256', '-extfile', '/tls/issuer.ext']);
writeFileSync('/public-ca/qa-ca.pem', readFileSync('/tls/ca.pem'), { mode: 0o644 });
const { privateKey, publicKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
const kid = 'lup-home-stage-only';
const jwk = { ...publicKey.export({ format: 'jwk' }), kid, alg: 'RS256', use: 'sig' };
const encode = value => Buffer.from(JSON.stringify(value)).toString('base64url');
const identities = [
    { key: 'teacher', sub: 'cccccccc-3333-4333-8333-333333333333', name: 'QA Underviser', role: 'Teacher' },
    { key: 'pupil', sub: 'aaaaaaaa-1111-4111-8111-111111111111', name: 'QA Elev', role: 'Student' },
    { key: 'peer', sub: 'bbbbbbbb-2222-4222-8222-222222222222', name: 'QA Anden elev', role: 'Student' },
    { key: 'outsider', sub: 'dddddddd-4444-4444-8444-444444444444', name: 'QA Anden underviser', role: 'Teacher' }
].map(identity => {
    const payload = encode({ sub: identity.sub, name: identity.name, role: identity.role, iss: issuer,
        aud: audience, iat: Math.floor(Date.now() / 1000), exp: expiresAt });
    const unsigned = encode({ alg: 'RS256', typ: 'JWT', kid }) + '.' + payload;
    return { ...identity, expiresAt, token: unsigned + '.' + sign('RSA-SHA256', Buffer.from(unsigned), privateKey).toString('base64url') };
});

https.createServer({ key: readFileSync('/tls/issuer.key'), cert: readFileSync('/tls/issuer.pem') }, (request, response) => {
    const json = value => {
        response.writeHead(200, { 'content-type': 'application/json', 'cache-control': 'no-store' });
        response.end(JSON.stringify(value));
    };
    if (request.method !== 'GET') { response.writeHead(405).end(); return; }
    if (request.url === '/.well-known/openid-configuration') return json({
        issuer, jwks_uri: issuer + '/.well-known/jwks.json',
        id_token_signing_alg_values_supported: ['RS256']
    });
    if (request.url === '/.well-known/jwks.json') return json({ keys: [jwk] });
    if (request.url === '/_qa/identities') return json({ fixture: 'hjem-staging-synthetic-only', identities });
    if (request.url === '/health') return json({ fixture: 'hjem-staging-synthetic-only', status: 'ok' });
    response.writeHead(404).end();
}).listen(9443, '0.0.0.0', () => console.log('Synthetic staging issuer ready.'));
