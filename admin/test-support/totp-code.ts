import { createHmac } from "node:crypto";

const base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

export interface TotpCodeOptions {
  secret: string;
  timestamp?: number;
  digits?: number;
  period?: number;
  algorithm?: string;
}

export function generateTotpCode(options: TotpCodeOptions): string {
  const secretBytes = decodeBase32Secret(options.secret);
  const period = options.period ?? 30;
  const digits = options.digits ?? 6;
  const timestamp = options.timestamp ?? Date.now();
  const counter = Math.floor(timestamp / 1000 / period);
  const counterBytes = Buffer.alloc(8);
  counterBytes.writeBigUInt64BE(BigInt(counter));

  const hmac = createHmac(resolveAlgorithm(options.algorithm), secretBytes)
    .update(counterBytes)
    .digest();
  const offset = hmac[hmac.length - 1] & 0x0f;
  const binaryCode = (
    ((hmac[offset] & 0x7f) << 24) |
    ((hmac[offset + 1] & 0xff) << 16) |
    ((hmac[offset + 2] & 0xff) << 8) |
    (hmac[offset + 3] & 0xff)
  );

  return (binaryCode % 10 ** digits).toString().padStart(digits, "0");
}

export function matchesTotpCode(options: TotpCodeOptions & { code: string; skewSteps?: number }): boolean {
  const skewSteps = options.skewSteps ?? 1;
  const timestamp = options.timestamp ?? Date.now();

  for (let stepOffset = -skewSteps; stepOffset <= skewSteps; stepOffset += 1) {
    const candidate = generateTotpCode({
      secret: options.secret,
      timestamp: timestamp + (stepOffset * (options.period ?? 30) * 1000),
      digits: options.digits,
      period: options.period,
      algorithm: options.algorithm,
    });
    if (candidate === options.code) {
      return true;
    }
  }

  return false;
}

function decodeBase32Secret(secret: string): Buffer {
  const normalized = secret.replace(/\s+/g, "").toUpperCase();
  let bits = "";

  for (const character of normalized) {
    const value = base32Alphabet.indexOf(character);
    if (value < 0) {
      throw new Error(`Unsupported base32 character '${character}'.`);
    }

    bits += value.toString(2).padStart(5, "0");
  }

  const bytes: number[] = [];
  for (let index = 0; index + 8 <= bits.length; index += 8) {
    bytes.push(Number.parseInt(bits.slice(index, index + 8), 2));
  }

  return Buffer.from(bytes);
}

function resolveAlgorithm(algorithm?: string): string {
  switch ((algorithm ?? "SHA1").trim().toUpperCase()) {
    case "SHA1":
      return "sha1";
    case "SHA256":
      return "sha256";
    case "SHA512":
      return "sha512";
    default:
      throw new Error(`Unsupported TOTP algorithm '${algorithm}'.`);
  }
}
