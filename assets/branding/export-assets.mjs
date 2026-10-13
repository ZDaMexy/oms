import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// All runtime artwork is exported from the same, font-independent vector master.
const require = createRequire(import.meta.url);
const sharpArg = process.argv.indexOf('--sharp-path');
const sharp = require(sharpArg < 0 ? 'sharp' : process.argv[sharpArg + 1]);
const root = fileURLToPath(new URL('../../', import.meta.url));
const source = await readFile(new URL('./oms-logo.svg', import.meta.url), 'utf8');
const content = source.replace(/^[\s\S]*?<svg[^>]*>/, '').replace(/<\/svg>\s*$/, '');
const flatContent = content.replace(' filter="url(#shadow)"', '');
const svg = (body, size = 1024) => Buffer.from(
  `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 1024 1024">${body}</svg>`);
const textures = path.join(root, 'osu.Game/Resources/Textures/OMS');
await mkdir(textures, { recursive: true });

await sharp(Buffer.from(source)).png().toFile(path.join(textures, 'logo.png'));

// LogoAnimation reads reveal time from R and coverage from G, ignoring PNG alpha.
// The rim traces clockwise from 12 o'clock; the letters reveal from left to right.
const size = 1024;
const { data: mask } = await sharp(svg(flatContent)).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
const background = Buffer.alloc(size * size * 3);
const highlight = Buffer.alloc(size * size * 3);
const alphaAt = (x, y) => x < 0 || y < 0 || x >= size || y >= size ? 0 : mask[(y * size + x) * 4 + 3];
for (let y = 0; y < size; y++) {
  for (let x = 0; x < size; x++) {
    const i = (y * size + x) * 3;
    const alpha = alphaAt(x, y);
    const radius = Math.hypot(x - 512, y - 512);
    const phase = radius > 400
      ? ((Math.atan2(y - 512, x - 512) + Math.PI * 2.5) % (Math.PI * 2)) / (Math.PI * 2)
      : Math.min(1, Math.max(0, (x - 154) / 722));
    background[i] = highlight[i] = Math.round(8 + phase * 238);
    background[i + 1] = alpha;
    let exterior = alpha;
    for (const [dx, dy] of [[-4, 0], [4, 0], [0, -4], [0, 4], [-3, -3], [3, -3], [-3, 3], [3, 3]])
      exterior = Math.max(exterior, alphaAt(x + dx, y + dy));
    // The production intro draws its highlight before the fill, so outline outside it.
    highlight[i + 1] = exterior - alpha;
  }
}
for (const [name, data] of [['logo-intro-background', background], ['logo-intro-highlight', highlight]]) {
  await sharp(data, { raw: { width: size, height: size, channels: 3 } }).png()
    .toFile(path.join(textures, `${name}.png`));
}

const iconContent = `<defs><linearGradient id="pink" x2="0" y2="1"><stop stop-color="#ff66ab"/><stop offset="1" stop-color="#cc5289"/></linearGradient></defs>
<circle cx="512" cy="512" r="484" fill="url(#pink)"/>${flatContent}`;
const iconSizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
const iconImages = [];
for (const iconSize of iconSizes)
  iconImages.push(await sharp(svg(iconContent)).resize(iconSize, iconSize).png().toBuffer());

// PNG-backed ICO entries support Windows, SDL and the executable resource compiler.
const header = Buffer.alloc(6 + iconSizes.length * 16);
header.writeUInt16LE(1, 2);
header.writeUInt16LE(iconSizes.length, 4);
let offset = header.length;
for (let i = 0; i < iconSizes.length; i++) {
  const entry = 6 + i * 16;
  header[entry] = header[entry + 1] = iconSizes[i] === 256 ? 0 : iconSizes[i];
  header.writeUInt16LE(1, entry + 4);
  header.writeUInt16LE(32, entry + 6);
  header.writeUInt32LE(iconImages[i].length, entry + 8);
  header.writeUInt32LE(offset, entry + 12);
  offset += iconImages[i].length;
}
await writeFile(path.join(root, 'osu.Desktop/lazer.ico'), Buffer.concat([header, ...iconImages]));
await sharp(svg(iconContent)).png().toFile(fileURLToPath(new URL('./oms-badge.png', import.meta.url)));
console.log('Exported OMS menu/ripple artwork, two intro animation maps, badge and 9 Windows icon sizes.');
