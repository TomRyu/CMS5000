-- CMS-5000 보안 이전(B안): 클라이언트 직접 접근 차단, Edge Functions(service_role)만 접근.
-- 두 운영 테이블에 RLS를 켜되 정책을 두지 않으면 anon/authenticated는 전부 거부되고
-- service_role(함수 환경)은 RLS를 우회하므로 함수만 데이터에 접근할 수 있다.

-- 1) 기존 운영 테이블 잠금
alter table public.cms_users      enable row level security;
alter table public.cms_login_logs enable row level security;

-- 만약 과거에 anon/authenticated에 부여된 정책이 있다면 제거(존재할 때만)
do $$
declare p record;
begin
  for p in
    select policyname, tablename from pg_policies
    where schemaname = 'public' and tablename in ('cms_users','cms_login_logs')
  loop
    execute format('drop policy if exists %I on public.%I', p.policyname, p.tablename);
  end loop;
end $$;

-- 2) 서버측 로그인 실패 잠금 상태 테이블 (함수만 접근)
create table if not exists public.cms_login_attempts (
  username        text primary key,
  fail_count      int         not null default 0,
  first_fail_at   timestamptz not null default now(),
  locked_until    timestamptz
);
alter table public.cms_login_attempts enable row level security;

-- 참고: service_role 키는 RLS를 우회하므로 별도 정책 불필요.
-- anon/authenticated에는 어떤 정책도 부여하지 않아 기본 거부 상태가 된다.
