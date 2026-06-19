-- CMS-5000 Module/Channel 확장 (Module Config · Reference Config 지원)

-- 1. module 테이블: 설정 시각 (원본 MODULE.ConfigDate)
ALTER TABLE public.module
    ADD COLUMN IF NOT EXISTS configdate timestamp;

-- 2. general_channel: 채널 Reference 사용여부/대상 (원본 CHANNEL.ReferenceActivity/ReferenceID)
ALTER TABLE public.general_channel
    ADD COLUMN IF NOT EXISTS referenceactivity smallint DEFAULT 0,
    ADD COLUMN IF NOT EXISTS referenceid       smallint DEFAULT 0;
