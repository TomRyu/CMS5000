import { create, getNumericDate, verify } from "https://deno.land/x/djwt@v3.0.2/mod.ts";

const SECRET = Deno.env.get("JWT_SECRET");
if (!SECRET) console.warn("[jwt] JWT_SECRET 환경변수가 비어 있습니다.");

let keyPromise: Promise<CryptoKey> | null = null;
function getKey(): Promise<CryptoKey> {
  return (keyPromise ??= crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(SECRET ?? "dev-insecure-secret"),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign", "verify"],
  ));
}

export interface SessionClaims {
  sub: string;       // user id
  username: string;
  role: string;      // Operator | Maintenance | Expert | Admin
  exp?: number;
}

// 토큰 수명: 8시간 (클라이언트 세션 타임아웃과 별개의 상한)
const TOKEN_TTL_SECONDS = 8 * 60 * 60;

export async function signSession(claims: Omit<SessionClaims, "exp">): Promise<string> {
  const key = await getKey();
  return await create(
    { alg: "HS256", typ: "JWT" },
    { ...claims, exp: getNumericDate(TOKEN_TTL_SECONDS) },
    key,
  );
}

export async function verifySession(authHeader: string | null): Promise<SessionClaims | null> {
  if (!authHeader) return null;
  const token = authHeader.replace(/^Bearer\s+/i, "").trim();
  if (!token) return null;
  try {
    const key = await getKey();
    return (await verify(token, key)) as unknown as SessionClaims;
  } catch {
    return null;
  }
}
