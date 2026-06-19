-- CMS-5000 Rack 확장 스키마 (TCP/Serial/Waveform/Trend/Modbus)

-- 1. tcpip 테이블 (없으면 생성)
CREATE TABLE IF NOT EXISTS public.tcpip (
    tcpid  serial PRIMARY KEY,
    ipaddr varchar(20),
    port   int
);

-- 2. serial 테이블 (없으면 생성)
CREATE TABLE IF NOT EXISTS public.serial (
    serialid  serial PRIMARY KEY,
    port      smallint DEFAULT 0,
    baudrate  int      DEFAULT 0,
    databits  smallint DEFAULT 0,
    paritybit smallint DEFAULT 0,
    stopbit   smallint DEFAULT 0
);

-- 3. rack 테이블 추가 컬럼 (없으면 추가)
ALTER TABLE public.rack
    ADD COLUMN IF NOT EXISTS waveforminterval smallint DEFAULT 0,
    ADD COLUMN IF NOT EXISTS trend            smallint DEFAULT 0,
    ADD COLUMN IF NOT EXISTS statictrend      smallint DEFAULT 10,
    ADD COLUMN IF NOT EXISTS dynamictrend     smallint DEFAULT 10,
    ADD COLUMN IF NOT EXISTS localserial      int REFERENCES public.serial(serialid),
    ADD COLUMN IF NOT EXISTS srvtcp           int REFERENCES public.tcpip(tcpid),
    ADD COLUMN IF NOT EXISTS modbusmode       smallint DEFAULT 0,
    ADD COLUMN IF NOT EXISTS modbustcp        int REFERENCES public.tcpip(tcpid),
    ADD COLUMN IF NOT EXISTS modbusserial     int REFERENCES public.serial(serialid);
