import { describe, expect, it } from "vitest";
import { generateTotpCode, matchesTotpCode } from "./totp-code";

describe("generateTotpCode", () => {
  it("matches the RFC 6238 SHA1 test vector", () => {
    const code = generateTotpCode({
      secret: "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
      timestamp: 59_000,
      digits: 8,
      algorithm: "SHA1",
    });

    expect(code).toBe("94287082");
  });

  it("normalizes lower-case base32 secrets", () => {
    const code = generateTotpCode({
      secret: "gezdgnbv gy3tqojq gezdgnbv gy3tqojq",
      timestamp: 1_111_111_109_000,
      digits: 8,
      algorithm: "SHA1",
    });

    expect(code).toBe("07081804");
  });

  it("accepts neighboring time steps for browser-driven confirmation", () => {
    const code = generateTotpCode({
      secret: "JBSWY3DPEHPK3PXP",
      timestamp: 1_700_000_000_000,
    });

    const matches = matchesTotpCode({
      secret: "JBSWY3DPEHPK3PXP",
      code,
      timestamp: 1_700_000_029_000,
    });

    expect(matches).toBe(true);
  });
});
