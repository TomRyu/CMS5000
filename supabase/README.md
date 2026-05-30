# CMS-5000 Supabase 백엔드 (보안 이전 B안)

클라이언트에 박혀 있던 **service_role 키를 제거**하고, 민감 DB 작업을 Edge Functions(`api`) 뒤로 옮긴다.
service_role 키는 **함수 환경변수에만** 존재한다.

## 구성
- `functions/api/index.ts` — 단일 함수 라우터
  - `GET /health` (공개) · `POST /login` (공개)
  - `POST /change-password` · `POST /set-font-size` · `POST /logout-log` (본인)
  - `GET /users` · `POST /users`(생성/수정) · `POST /users/delete` · `GET /login-logs` (관리자)
- `functions/_shared/` — cors / jwt(커스텀 HS256) / admin(service_role 클라이언트) / throttle(서버측 잠금)
- `migrations/*_rls_lockdown.sql` — 두 테이블 RLS 잠금 + `cms_login_attempts` 생성

## 배포 절차 (사용자 실행 — 대화형 로그인 필요)

```bash
# 1) CLI 설치 (한 번만): https://supabase.com/docs/guides/cli
#    scoop install supabase   또는   npm i -g supabase

# 2) 로그인 + 프로젝트 링크  (브라우저 인증)
supabase login
supabase link --project-ref xfoipljsksrqgrwzqhza

# 3) 함수 시크릿 설정 (service_role 키와 JWT 서명용 비밀)
supabase secrets set SERVICE_ROLE_KEY="<현재 service_role 키>"
supabase secrets set JWT_SECRET="<충분히 긴 무작위 문자열>"
#   JWT_SECRET 예시 생성:  openssl rand -base64 48

# 4) DB 마이그레이션 적용 (RLS 잠금 + 잠금테이블)
supabase db push

# 5) 함수 배포 (플랫폼 JWT 검증 끔 — config.toml에도 명시됨)
supabase functions deploy api --no-verify-jwt
```

배포 후 함수 URL: `https://xfoipljsksrqgrwzqhza.supabase.co/functions/v1/api`

## 컷오버 순서 (중요)
1. 위 4·5단계로 함수/RLS 적용
2. 수정된 클라이언트(anon 키만 탑재) 릴리스
3. **service_role 키 회전** (대시보드 → Settings → API) → 구버전 클라이언트 접속 차단(=강제 업데이트)
   - 회전 후 3단계 `supabase secrets set SERVICE_ROLE_KEY=...`를 **새 키로 다시 설정**해야 함.

## 동작 확인
```bash
curl https://xfoipljsksrqgrwzqhza.supabase.co/functions/v1/api/health \
  -H "apikey: <anon 키>"
# => {"ok":true}
```
