-- ============================================================
-- CMS-5000 로컬 PostgreSQL 초기 설정 SQL
-- 대상 서버: 192.168.0.3  데이터베이스: CMS5000
-- psql 또는 pgAdmin SQL 편집기에서 실행하세요
-- ============================================================

-- 1. 사용자 테이블
CREATE TABLE IF NOT EXISTS public.cms_users (
    id            uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    username      text        UNIQUE NOT NULL,
    password_hash text        NOT NULL,
    role          text        NOT NULL CHECK (role IN ('Operator','Maintenance','Expert','Admin')),
    display_name  text        NOT NULL,
    is_active     boolean     NOT NULL DEFAULT true,
    font_size     text,
    created_at    timestamptz NOT NULL DEFAULT now()
);

-- 2. 로그인/로그아웃 이력 테이블
CREATE TABLE IF NOT EXISTS public.cms_login_logs (
    id           uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      uuid,
    username     text        NOT NULL,
    display_name text,
    role         text,
    action       text        NOT NULL,   -- 'login' | 'logout'
    logged_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_cms_login_logs_logged_at
    ON public.cms_login_logs (logged_at DESC);

-- 3. 로그인 실패 잠금 테이블 (브루트포스 방어)
CREATE TABLE IF NOT EXISTS public.cms_login_attempts (
    username      text        PRIMARY KEY,
    fail_count    int         NOT NULL DEFAULT 0,
    first_fail_at timestamptz NOT NULL DEFAULT now(),
    locked_until  timestamptz
);

-- 4. 최초 관리자 계정 생성
--    비밀번호 "admin1234" 의 BCrypt 해시 (rounds=11)
--    앱 첫 실행 후 관리자 페이지에서 비밀번호를 반드시 변경하세요
INSERT INTO public.cms_users (username, password_hash, role, display_name)
VALUES (
    'admin',
    '$2a$11$nC6uUNxwLNMzKrP.P.z5XeZTnvNd7KNDCz05x5nsHUDz7vcFx1zhO',
    'Admin',
    '시스템 관리자'
)
ON CONFLICT (username) DO NOTHING;

-- 5. 확인
SELECT id, username, role, display_name, is_active, created_at
FROM public.cms_users;
