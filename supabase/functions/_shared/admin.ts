import { createClient, SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";

// service_role 키는 함수 환경변수에만 존재(클라이언트에 배포되지 않음).
// 이 클라이언트는 RLS를 우회하므로 절대 클라이언트로 노출 금지.
let client: SupabaseClient | null = null;

export function admin(): SupabaseClient {
  if (client) return client;
  const url = Deno.env.get("SUPABASE_URL");
  const serviceKey = Deno.env.get("SERVICE_ROLE_KEY") ?? Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!url || !serviceKey) throw new Error("SUPABASE_URL / SERVICE_ROLE_KEY 환경변수가 필요합니다.");
  client = createClient(url, serviceKey, { auth: { persistSession: false } });
  return client;
}
