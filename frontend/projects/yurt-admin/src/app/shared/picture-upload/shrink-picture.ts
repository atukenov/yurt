/** Files at or below this size and side length are sent as they are. */
const KEEP_BYTES = 600 * 1024;

/**
 * Makes a phone photo small enough to upload (the server takes at most 2 MB): scales it so the longest side is at
 * most `maxSide` pixels and saves it again. Photos become JPEG; PNG and WebP keep their transparency as WebP when the
 * browser can write WebP, else PNG. A small file is returned unchanged.
 */
export async function shrinkPicture(file: Blob, maxSide: number): Promise<Blob> {
  const bitmap = await createImageBitmap(file);
  try {
    const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
    if (scale === 1 && file.size <= KEEP_BYTES) return file;

    const canvas = document.createElement('canvas');
    canvas.width = Math.max(1, Math.round(bitmap.width * scale));
    canvas.height = Math.max(1, Math.round(bitmap.height * scale));
    const ctx = canvas.getContext('2d');
    if (!ctx) return file;
    ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);

    const photo = file.type === 'image/jpeg';
    const first = await toBlob(canvas, photo ? 'image/jpeg' : 'image/webp', 0.85);
    // Older Safari writes PNG when asked for WebP; that is fine, only an empty result is not.
    const out = first ?? (await toBlob(canvas, 'image/png'));
    return out && out.size < file.size ? out : file;
  } finally {
    bitmap.close();
  }
}

function toBlob(canvas: HTMLCanvasElement, type: string, quality?: number): Promise<Blob | null> {
  return new Promise((resolve) => canvas.toBlob(resolve, type, quality));
}
