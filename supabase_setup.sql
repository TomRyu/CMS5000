-- ============================================================
-- CMS-5000 Supabase 초기 설정 SQL
-- Supabase Dashboard > SQL Editor 에서 실행하세요
-- ============================================================

-- 1. 사용자 테이블 생성
create table if not exists public.cms_users (
  id           uuid        primary key default gen_random_uuid(),
  username     text        unique not null,
  password_hash text       not null,
  role         text        not null check (role in ('Operator','Maintenance','Expert','Admin')),
  display_name text        not null,
  is_active    boolean     not null default true,
  created_at   timestamptz not null default now()
);

-- 2. RLS 비활성화 (서비스 롤 키로만 접근)
alter table public.cms_users disable row level security;

-- 3. 최초 관리자 계정 생성
--    비밀번호 "admin1234" 의 BCrypt 해시 (rounds=11)
--    앱 첫 실행 후 관리자 페이지에서 비밀번호를 반드시 변경하세요
insert into public.cms_users (username, password_hash, role, display_name)
values (
  'admin',
  '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi',
  'Admin',
  '시스템 관리자'
)
on conflict (username) do nothing;

-- 4. 확인
select id, username, role, display_name, is_active, created_at
from public.cms_users;
