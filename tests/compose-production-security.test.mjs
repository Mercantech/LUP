import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmdirSync, unlinkSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { after, test } from 'node:test';
import { fileURLToPath } from 'node:url';

const repository = fileURLToPath(new URL('..', import.meta.url));
const temporary = mkdtempSync(join(tmpdir(), 'lup-compose-security-'));
const environmentFile = join(temporary, 'empty.env');
writeFileSync(environmentFile, '');
after(() => {
  unlinkSync(environmentFile);
  rmdirSync(temporary);
});

function configuration({ production = true, inherited = '', credentials = {} } = {}) {
  const environment = {
    ...process.env,
    MINIO_ROOT_USER: 'storage-test-user',
    MINIO_ROOT_PASSWORD: 'storage-test-password',
    ...credentials,
  };
  const args = [
    'compose', '--project-name', 'lup-storage-security', '--env-file', environmentFile,
    '-f', 'docker-compose.yml',
  ];
  if (inherited) args.push('-f', '-');
  if (production) args.push('-f', 'docker-compose.production.yml');
  args.push('config', '--format', 'json');
  return spawnSync('docker', args, {
    cwd: repository, env: environment, input: inherited, encoding: 'utf8',
    timeout: 30_000, windowsHide: true,
  });
}

test('production keeps the original data volumes and private API storage settings', () => {
  const baseline = configuration({ production: false });
  assert.equal(baseline.status, 0, baseline.stderr);
  const result = configuration();
  assert.equal(result.status, 0, result.stderr);
  const original = JSON.parse(baseline.stdout);
  const config = JSON.parse(result.stdout);
  const storage = config.services.minio;
  assert.deepEqual(Object.keys(storage.networks), ['default']);
  assert.deepEqual(storage.networks.default.aliases, ['mercantec-tech-minio']);
  assert.deepEqual(storage.labels, { 'traefik.enable': 'false' });
  assert.equal(storage.ports?.length ?? 0, 0);
  assert.equal(storage.environment.MINIO_BROWSER_REDIRECT_URL, '');
  assert.equal(config.services.api.environment.ObjectStorage__Endpoint, 'http://mercantec-tech-minio:9000');
  assert.equal(config.services.api.environment.ObjectStorage__PublicBaseUrl, '');
  assert.equal(config.services.previews.environment.ObjectStorage__Endpoint, 'http://mercantec-tech-minio:9000');
  for (const name of ['mercantec_tech_pg', 'mercantec_tech_minio']) {
    assert.deepEqual(config.volumes[name], original.volumes[name]);
  }
  for (const name of ['db', 'minio']) {
    assert.deepEqual(config.services[name].volumes, original.services[name].volumes);
  }
  assert.equal(storage.environment.MINIO_ROOT_USER, config.services['minio-init'].environment.MINIO_ROOT_USER);
  assert.equal(storage.environment.MINIO_ROOT_PASSWORD, config.services['minio-init'].environment.MINIO_ROOT_PASSWORD);
  for (const name of ['api', 'previews']) {
    assert.equal(config.services[name].environment.ObjectStorage__AccessKey, storage.environment.MINIO_ROOT_USER);
    assert.equal(config.services[name].environment.ObjectStorage__SecretKey, storage.environment.MINIO_ROOT_PASSWORD);
  }
});

test('production overrides inherited MinIO edge networks, routers and published ports', () => {
  const result = configuration({ inherited: `
services:
  minio:
    networks:
      dokploy-network: {}
    labels:
      traefik.enable: "true"
      traefik.http.routers.storage-public.rule: Host(\"storage.example.test\")
    ports:
      - "9000:9000"
      - "9001:9001"
` });
  assert.equal(result.status, 0, result.stderr);
  const storage = JSON.parse(result.stdout).services.minio;
  assert.deepEqual(Object.keys(storage.networks), ['default']);
  assert.deepEqual(storage.labels, { 'traefik.enable': 'false' });
  assert.equal(storage.ports?.length ?? 0, 0);
});

for (const variable of ['MINIO_ROOT_USER', 'MINIO_ROOT_PASSWORD']) {
  for (const value of [undefined, '']) {
    test(`production rejects ${value === undefined ? 'missing' : 'empty'} ${variable}`, () => {
      const result = configuration({ credentials: { [variable]: value } });
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, new RegExp(variable));
    });
  }
}
