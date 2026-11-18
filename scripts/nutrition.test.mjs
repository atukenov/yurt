import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const read = (p) => readFileSync(join(repo, p), 'utf8');

const codesIn = (src, re) => [...re.exec(src)[1].matchAll(/"([a-z_]+)"|'([a-z_]+)'/g)].map((m) => m[1] ?? m[2]);
const backend = codesIn(read('backend/src/Yurt.Application/Features/Menu/Nutrition.cs'), /AllergenCodes =\s*\[([^\]]*)\]/);
const frontend = codesIn(read('frontend/projects/shared-models/src/lib/models.ts'), /ALLERGEN_CODES = \[([^\]]*)\]/);

test('backend and frontend know the same allergen codes', () => {
  assert.equal(backend.length, 14);
  assert.deepEqual(frontend, backend);
});

for (const app of ['customer', 'admin']) {
  test(`${app}: every allergen has a name in en, ru and kk`, () => {
    const src = read(`frontend/projects/yurt-${app}/src/app/core/lang.service.ts`);
    for (const lang of ['en', 'ru', 'kk']) {
      const start = src.indexOf(`\n  ${lang}: {\n`);
      const block = src.slice(start, src.indexOf('\n  },', start));
      for (const code of backend) assert.ok(block.includes(`'allergen.${code}':`), `${app}/${lang}: allergen.${code}`);
    }
  });
}
