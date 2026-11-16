// 기능: assets/icon의 SVG 원본에서 png 크기별 파일과 hunsu.ico를 만든다. 16~48px는 단순판(hunsu-small.svg), 64px 이상은 전체판(hunsu.svg).
// 사용법: node tools/icons/make_app_icon.mjs   (저장소 루트에서. 의존: playwright(chromium), ImageMagick `convert`)
// 의존: 결과는 assets/icon/png/*.png, assets/icon/hunsu.ico. 앱 아이콘은 src/ClockOut.Hunsu/Assets/hunsu.ico로 복사해 쓴다.
import { createRequire } from 'node:module';
const { chromium } = createRequire(import.meta.url)('playwright');
import { readFileSync, mkdirSync, copyFileSync } from 'node:fs';
import { execFileSync } from 'node:child_process';

const dir = 'assets/icon', out = `${dir}/png`;
mkdirSync(out, { recursive: true });
const sizes = [16, 24, 32, 48, 64, 128, 256, 512];
const pick = (s) => (s <= 48 ? 'hunsu-small.svg' : 'hunsu.svg');
const browser = await chromium.launch();
const page = await browser.newPage();
for (const s of sizes) {
  const svg = readFileSync(`${dir}/${pick(s)}`, 'utf8').replace('<svg', `<svg width="${s}" height="${s}"`);
  await page.setViewportSize({ width: s, height: s });
  await page.setContent(`<body style="margin:0;background:transparent">${svg}</body>`);
  await page.screenshot({ path: `${out}/hunsu-${s}.png`, omitBackground: true });
}
await browser.close();
const icoParts = [16, 24, 32, 48, 64, 128, 256].map((s) => `${out}/hunsu-${s}.png`);
execFileSync('convert', [...icoParts, `${dir}/hunsu.ico`]);
mkdirSync('src/ClockOut.Hunsu/Assets', { recursive: true });
copyFileSync(`${dir}/hunsu.ico`, 'src/ClockOut.Hunsu/Assets/hunsu.ico');
console.log('완료: assets/icon/hunsu.ico, assets/icon/png/');
