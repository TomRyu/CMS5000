-- CMS-5000 Reference Config(원본 frmReference) 채널 신호설정 스키마

CREATE TABLE IF NOT EXISTS public.channel_reference (
    stationid int  NOT NULL,
    rackid    int  NOT NULL,
    moduleid  int  NOT NULL,
    channelid int  NOT NULL,

    -- 헤더
    name             varchar(64) DEFAULT '',
    channeltype      int     DEFAULT 0,
    activitymode     smallint DEFAULT 0,   -- 0 Inactivity / 1 Activity / 2 Simulated
    assign           smallint DEFAULT 0,

    -- INFO 탭
    reassignmode     smallint DEFAULT 0,   -- 0 Alternate Reference / 1 Simulated Speed
    speed            int     DEFAULT 0,
    alternateid      smallint DEFAULT 0,   -- 1~4 (콤보 인덱스)
    rotationdir      smallint DEFAULT 0,   -- 0 CW / 1 CCW
    signalpolarity   smallint DEFAULT 0,   -- 0 Projection / 1 Notch
    thresholdtype    smallint DEFAULT 0,   -- 0 Manual / 1 Auto
    thresholdlevel   int     DEFAULT 0,
    clampvalue       int     DEFAULT 0,
    upperlimit       int     DEFAULT 0,
    hysteresislevel  int     DEFAULT 0,
    fluctuationrange int     DEFAULT 0,
    unalteredtime    int     DEFAULT 0,
    orientationangle int     DEFAULT 0,
    orientation      smallint DEFAULT 0,   -- 0 Left / 1 Right
    waveforminterval int     DEFAULT 0,
    eprevolution     int     DEFAULT 0,

    -- Sensor 탭
    sensorname       varchar(64) DEFAULT '',
    sensitivity      int     DEFAULT 0,
    sensorunit       varchar(32) DEFAULT '',
    icp              smallint DEFAULT 0,   -- 0 OFF / 1 ON
    powerlow         int     DEFAULT 0,
    powerhigh        int     DEFAULT 0,
    proximitorpower  smallint DEFAULT 0,   -- 0 -18V / 1 -24V
    signaltype       smallint DEFAULT 0,   -- 0 Magnetic / 1 Proximitor

    -- Auto Upload 탭
    uploadtime       int     DEFAULT 0,
    uploadcondition  smallint DEFAULT 0,   -- 0 None / 1 Time / 2 RPM / 3 Both
    startuprpm       int     DEFAULT 0,
    shutdownrpm      int     DEFAULT 0,

    -- Min/Max/Delta 탭
    sr_begin         int     DEFAULT 0,
    sr_end           int     DEFAULT 0,
    sr_delta         int     DEFAULT 0,
    sd_max           int     DEFAULT 0,
    sd_min           int     DEFAULT 0,
    sd_delta         int     DEFAULT 0,
    su_max           int     DEFAULT 0,
    su_min           int     DEFAULT 0,
    su_delta         int     DEFAULT 0,

    PRIMARY KEY (stationid, rackid, moduleid, channelid)
);
