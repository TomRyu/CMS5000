import { admin } from "./admin.ts";

// 서버측 로그인 실패 잠금(무차별 대입 방어). cms_login_attempts 테이블에 상태 보존.
const MAX_ATTEMPTS = 5;       // 잠금까지 허용 실패
const LOCKOUT_MIN = 5;        // 잠금 시간(분)
const WINDOW_MIN = 15;        // 실패 누적 창(분)

/** 잠겨 있으면 남은 초를 반환, 아니면 null. */
export async function getRemainingLockSeconds(username: string): Promise<number | null> {
  const { data } = await admin()
    .from("cms_login_attempts")
    .select("locked_until")
    .eq("username", username)
    .maybeSingle();

  if (!data?.locked_until) return null;
  const remainingMs = new Date(data.locked_until).getTime() - Date.now();
  return remainingMs > 0 ? Math.ceil(remainingMs / 1000) : null;
}

export async function recordFailure(username: string): Promise<void> {
  const db = admin();
  const { data } = await db
    .from("cms_login_attempts")
    .select("fail_count, first_fail_at")
    .eq("username", username)
    .maybeSingle();

  const now = Date.now();
  let failCount = (data?.fail_count ?? 0) + 1;
  let firstFailAt = data?.first_fail_at ? new Date(data.first_fail_at).getTime() : now;

  // 누적 창을 벗어났으면 카운트 리셋
  if (now - firstFailAt > WINDOW_MIN * 60_000) {
    failCount = 1;
    firstFailAt = now;
  }

  const lockedUntil = failCount >= MAX_ATTEMPTS
    ? new Date(now + LOCKOUT_MIN * 60_000).toISOString()
    : null;

  await db.from("cms_login_attempts").upsert({
    username,
    fail_count: failCount,
    first_fail_at: new Date(firstFailAt).toISOString(),
    locked_until: lockedUntil,
  });
}

export async function resetAttempts(username: string): Promise<void> {
  await admin().from("cms_login_attempts").delete().eq("username", username);
}
