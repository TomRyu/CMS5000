// 순수 JS 구현(Web Worker 비의존) → Supabase Edge 런타임에서 안전. .NET BCrypt.Net($2a/$2b)과 호환.
import bcrypt from "npm:bcryptjs@2.4.3";
import { corsHeaders, error, json } from "../_shared/cors.ts";
import { admin } from "../_shared/admin.ts";
import { signSession, verifySession } from "../_shared/jwt.ts";
import { getRemainingLockSeconds, recordFailure, resetAttempts } from "../_shared/throttle.ts";

// deno-lint-ignore no-explicit-any
function mapUser(r: any) {
  return {
    id: r.id,
    username: r.username,
    role: r.role,
    displayName: r.display_name,
    isActive: r.is_active,
    createdAt: r.created_at,
    fontSize: r.font_size,
  };
}

async function recordLog(user: { id: string; username: string; display_name?: string; displayName?: string; role: string }, action: string) {
  await admin().from("cms_login_logs").insert({
    user_id: user.id,
    username: user.username,
    display_name: (user as any).display_name ?? (user as any).displayName ?? "",
    role: user.role,
    action,
    logged_at: new Date().toISOString(),
  });
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response("ok", { headers: corsHeaders });

  const url = new URL(req.url);
  let route = url.pathname;
  const i = route.indexOf("/api");
  if (i >= 0) route = route.slice(i + 4);
  route = route.replace(/\/+$/, "") || "/";

  try {
    // ── 공개: 헬스체크 ──────────────────────────────
    if (req.method === "GET" && (route === "/" || route === "/health")) {
      return json({ ok: true });
    }

    // ── 공개: 로그인 ────────────────────────────────
    if (req.method === "POST" && route === "/login") {
      const { username, password } = await req.json();
      if (!username || !password) return error("아이디와 비밀번호를 입력하세요.");

      const lockSec = await getRemainingLockSeconds(username);
      if (lockSec) {
        const mins = Math.max(1, Math.ceil(lockSec / 60));
        return error(`로그인 시도가 많아 일시적으로 잠겼습니다. 약 ${mins}분 후 다시 시도하세요.`, 429);
      }

      const { data: user } = await admin()
        .from("cms_users").select("*").eq("username", username).maybeSingle();

      if (!user) {
        await recordFailure(username);
        return error("아이디 또는 비밀번호가 올바르지 않습니다.", 401);
      }
      if (!user.is_active) return error("비활성화된 계정입니다. 관리자에게 문의하세요.", 403);

      if (!bcrypt.compareSync(password, user.password_hash)) {
        await recordFailure(username);
        return error("아이디 또는 비밀번호가 올바르지 않습니다.", 401);
      }

      await resetAttempts(username);
      await recordLog(user, "login");
      const token = await signSession({ sub: user.id, username: user.username, role: user.role });
      return json({ token, user: mapUser(user) });
    }

    // 이하 인증 필요
    const claims = await verifySession(req.headers.get("authorization"));
    if (!claims) return error("인증이 필요합니다.", 401);
    const isAdmin = claims.role === "Admin";

    // ── 본인: 로그아웃 이력 ─────────────────────────
    if (req.method === "POST" && route === "/logout-log") {
      await recordLog({ id: claims.sub, username: claims.username, role: claims.role }, "logout");
      return json({ ok: true });
    }

    // ── 본인: 글자 크기 저장 ────────────────────────
    if (req.method === "POST" && route === "/set-font-size") {
      const { fontSize } = await req.json();
      await admin().from("cms_users").update({ font_size: fontSize }).eq("id", claims.sub);
      return json({ ok: true });
    }

    // ── 본인: 비밀번호 변경 ─────────────────────────
    if (req.method === "POST" && route === "/change-password") {
      const { currentPassword, newPassword, confirmPassword } = await req.json();
      if (!currentPassword || !newPassword) return error("모든 항목을 입력하세요.");
      if (newPassword.length < 8) return error("새 비밀번호는 8자 이상이어야 합니다.");
      if (newPassword !== confirmPassword) return error("새 비밀번호가 일치하지 않습니다.");

      const { data: me } = await admin()
        .from("cms_users").select("password_hash").eq("id", claims.sub).maybeSingle();
      if (!me) return error("사용자를 찾을 수 없습니다.", 404);
      if (!bcrypt.compareSync(currentPassword, me.password_hash)) return error("현재 비밀번호가 올바르지 않습니다.");
      if (bcrypt.compareSync(newPassword, me.password_hash)) return error("기존과 다른 비밀번호를 사용하세요.");

      const hash = bcrypt.hashSync(newPassword, 11);
      await admin().from("cms_users").update({ password_hash: hash }).eq("id", claims.sub);
      return json({ ok: true });
    }

    // ── 관리자: 사용자 목록 ─────────────────────────
    if (req.method === "GET" && route === "/users") {
      if (!isAdmin) return error("권한이 없습니다.", 403);
      const { data } = await admin()
        .from("cms_users").select("*").order("created_at", { ascending: true });
      return json((data ?? []).map(mapUser));
    }

    // ── 관리자: 사용자 생성/수정 ────────────────────
    if (req.method === "POST" && route === "/users") {
      if (!isAdmin) return error("권한이 없습니다.", 403);
      const b = await req.json();

      if (b.id) {
        // 수정
        const patch: Record<string, unknown> = {};
        if (b.displayName !== undefined) patch.display_name = b.displayName;
        if (b.role !== undefined) patch.role = b.role;
        if (b.isActive !== undefined) patch.is_active = b.isActive;
        if (b.password) patch.password_hash = bcrypt.hashSync(b.password, 11);
        const { data, error: e } = await admin()
          .from("cms_users").update(patch).eq("id", b.id).select("*").maybeSingle();
        if (e) return error(e.message, 400);
        return json(mapUser(data));
      } else {
        // 생성
        if (!b.username || !b.displayName || !b.password) return error("아이디·이름·비밀번호를 입력하세요.");
        const { data, error: e } = await admin().from("cms_users").insert({
          username: b.username,
          display_name: b.displayName,
          role: b.role ?? "Operator",
          is_active: true,
          password_hash: bcrypt.hashSync(b.password, 11),
        }).select("*").maybeSingle();
        if (e) return error(e.message, 400);
        return json(mapUser(data));
      }
    }

    // ── 관리자: 사용자 삭제 ─────────────────────────
    if (req.method === "POST" && route === "/users/delete") {
      if (!isAdmin) return error("권한이 없습니다.", 403);
      const { id } = await req.json();
      if (id === claims.sub) return error("현재 로그인된 계정은 삭제할 수 없습니다.");
      const { error: e } = await admin().from("cms_users").delete().eq("id", id);
      if (e) return error(e.message, 400);
      return json({ ok: true });
    }

    // ── 관리자: 로그인 이력 ─────────────────────────
    if (req.method === "GET" && route === "/login-logs") {
      if (!isAdmin) return error("권한이 없습니다.", 403);
      const limit = Number(url.searchParams.get("limit") ?? "200");
      const { data } = await admin()
        .from("cms_login_logs").select("*")
        .order("logged_at", { ascending: false })
        .limit(limit);
      // deno-lint-ignore no-explicit-any
      return json((data ?? []).map((r: any) => ({
        id: r.id, userId: r.user_id, username: r.username, displayName: r.display_name,
        role: r.role, action: r.action, loggedAt: r.logged_at,
      })));
    }

    return error("알 수 없는 요청입니다.", 404);
  } catch (e) {
    return error(`서버 오류: ${e instanceof Error ? e.message : String(e)}`, 500);
  }
});
