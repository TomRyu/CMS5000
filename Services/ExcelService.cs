using ClosedXML.Excel;
using Npgsql;

namespace CMS5000.Services;

public static class ExcelService
{
    // 설정 저장: 핵심 설정 테이블만 (Junction 제외)
    private static readonly string[] SettingsTables =
    [
        "module_type", "channel_type", "sensor", "sensor_unit",
        "display_plot", "proportional", "display_scale_range", "event"
    ];

    // 설정 DB 백업: 핵심 설정 + 모든 Junction 테이블
    private static readonly string[] ConfigBackupTables =
    [
        "module_type", "channel_type", "sensor", "sensor_unit",
        "display_plot", "proportional", "display_scale_range", "event",
        "channel_type_sensor", "channel_type_sensor_unit",
        "channel_type_display_plot", "channel_type_proportional",
        "channel_type_display_scale_range",
        "display_plot_proportional", "plot_data_source",
        "plot_compensation", "plot_freq_analysis",
        "proportional_display_scale_range"
    ];

    // 전체 DB 백업: 장치 구조 + 설정 + Junction (FK 순서)
    private static readonly string[] FullBackupTables =
    [
        "station", "train", "rack", "module", "channel",
        "module_type", "channel_type", "sensor", "sensor_unit",
        "display_plot", "proportional", "display_scale_range", "event",
        "channel_type_sensor", "channel_type_sensor_unit",
        "channel_type_display_plot", "channel_type_proportional",
        "channel_type_display_scale_range",
        "display_plot_proportional", "plot_data_source",
        "plot_compensation", "plot_freq_analysis",
        "proportional_display_scale_range"
    ];

    // ── Check Data ─────────────────────────────────────────────────────────
    public static async Task<(bool ok, List<string> messages)> CheckDataAsync()
    {
        var msgs = new List<string>();
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        await AddCheckAsync(conn, msgs,
            "SELECT COUNT(*) FROM public.sensor WHERE unit IS NULL",
            n => n > 0 ? $"[센서] 단위 미지정 센서: {n}개" : null);

        await AddCheckAsync(conn, msgs,
            """
            SELECT COUNT(*) FROM public.channel_type ct
            WHERE NOT EXISTS (
                SELECT 1 FROM public.channel_type_sensor s WHERE s.channeltype = ct.channeltype
            )
            """,
            n => n > 0 ? $"[채널타입] 연결된 센서 없는 채널타입: {n}개" : null);

        await AddCheckAsync(conn, msgs,
            """
            SELECT COUNT(*) FROM public.display_plot dp
            WHERE NOT EXISTS (
                SELECT 1 FROM public.display_plot_proportional p WHERE p.plotid = dp.plotid
            )
            """,
            n => n > 0 ? $"[Display Plot] 비례값 미연결 Plot: {n}개" : null);

        await AddCheckAsync(conn, msgs,
            """
            SELECT COUNT(*) FROM public.proportional p
            WHERE NOT EXISTS (
                SELECT 1 FROM public.proportional_display_scale_range s WHERE s.varid = p.varid
            )
            """,
            n => n > 0 ? $"[비례값] Scale Range 미연결 비례값: {n}개" : null);

        await AddCheckAsync(conn, msgs,
            """
            SELECT COUNT(*) FROM public.module m
            WHERE m.moduletype IS NOT NULL
              AND NOT EXISTS (
                SELECT 1 FROM public.module_type mt WHERE mt.moduletype = m.moduletype
              )
            """,
            n => n > 0 ? $"[모듈] 모듈타입 FK 불일치: {n}개" : null);

        bool ok = msgs.Count == 0;
        if (ok) msgs.Add("이상 없음 — 데이터 무결성 확인 완료.");
        else msgs.Insert(0, $"총 {msgs.Count}개 항목에서 이상이 발견되었습니다:\n");
        return (ok, msgs);
    }

    private static async Task AddCheckAsync(
        NpgsqlConnection conn, List<string> msgs, string sql, Func<int, string?> msgFn)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var n = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            var msg = msgFn(n);
            if (msg != null) msgs.Add("  • " + msg);
        }
        catch (Exception ex)
        {
            msgs.Add($"  • [오류] {ex.Message}");
        }
    }

    // ── 설정 저장 (XLSX) ────────────────────────────────────────────────────
    public static async Task ExportSettingsAsync(string path) =>
        await ExportTablesAsync(path, SettingsTables);

    // ── 설정 DB 백업 (XLSX) ─────────────────────────────────────────────────
    public static async Task BackupConfigDbAsync(string path) =>
        await ExportTablesAsync(path, ConfigBackupTables);

    // ── 전체 DB 백업 ────────────────────────────────────────────────────────
    public static async Task BackupAllDbAsync(string path) =>
        await ExportTablesAsync(path, FullBackupTables);

    // ── 설정 로딩 (Import) ──────────────────────────────────────────────────
    public static async Task<(int imported, int skipped, List<string> errors)> ImportSettingsAsync(string path)
    {
        int imported = 0, skipped = 0;
        var errors = new List<string>();

        using var wb = new XLWorkbook(path);
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        foreach (var ws in wb.Worksheets)
        {
            try
            {
                var count = await ImportSheetAsync(conn, ws);
                if (count >= 0) imported++;
                else skipped++;
            }
            catch (Exception ex)
            {
                errors.Add($"{ws.Name}: {ex.Message}");
                skipped++;
            }
        }
        return (imported, skipped, errors);
    }

    // ── 전체 DB 복원 ────────────────────────────────────────────────────────
    public static async Task<(int imported, int skipped, List<string> errors)> RestoreAllDbAsync(string path) =>
        await ImportSettingsAsync(path);

    // ─── Private helpers ───────────────────────────────────────────────────

    private static async Task ExportTablesAsync(string path, string[] tables)
    {
        using var wb = new XLWorkbook();
        await using var conn = await PostgresService.DataSource.OpenConnectionAsync();

        foreach (var tbl in tables)
        {
            try { await AddTableSheetAsync(wb, conn, tbl); }
            catch { /* 테이블 없으면 건너뜀 */ }
        }

        if (!wb.Worksheets.Any())
            throw new InvalidOperationException("내보낼 데이터가 없습니다.");

        wb.SaveAs(path);
    }

    private static async Task AddTableSheetAsync(XLWorkbook wb, NpgsqlConnection conn, string tableName)
    {
        var sheetName = tableName.Length > 31 ? tableName[..31] : tableName;
        var ws = wb.Worksheets.Add(sheetName);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT * FROM public.\"{tableName}\"";
        await using var r = await cmd.ExecuteReaderAsync();

        for (int c = 0; c < r.FieldCount; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = r.GetName(c);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        while (await r.ReadAsync())
        {
            for (int c = 0; c < r.FieldCount; c++)
                WriteCellValue(ws.Cell(row, c + 1), r.GetValue(c));
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void WriteCellValue(IXLCell cell, object val)
    {
        if (val is DBNull || val is null) { cell.Value = ""; return; }
        cell.Value = val switch
        {
            short   s  => (XLCellValue)(int)s,
            int     i  => (XLCellValue)i,
            long    l  => (XLCellValue)(double)l,
            float   f  => (XLCellValue)(double)f,
            double  d  => (XLCellValue)d,
            decimal m  => (XLCellValue)(double)m,
            bool    b  => (XLCellValue)b,
            DateTime dt=> (XLCellValue)dt,
            _          => (XLCellValue)(val.ToString() ?? "")
        };
    }

    private static async Task<int> ImportSheetAsync(NpgsqlConnection conn, IXLWorksheet ws)
    {
        var usedRows = ws.RowsUsed().ToList();
        if (usedRows.Count < 2) return -1;

        var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastCol == 0) return -1;

        var headers = usedRows[0].Cells(1, lastCol)
                                 .Select(c => c.GetString().Trim())
                                 .TakeWhile(s => !string.IsNullOrEmpty(s))
                                 .ToList();
        if (headers.Count == 0) return -1;

        var tableName = ws.Name;
        var colTypes  = await GetColumnTypesAsync(conn, tableName);

        var colNames = string.Join(", ", headers.Select(h => $"\"{h}\""));
        var colVals  = string.Join(", ", headers.Select((h, i) =>
        {
            var pgType = colTypes.GetValueOrDefault(h.ToLower(), "text");
            return $"@p{i}::{MapCastType(pgType)}";
        }));
        var sql = $"INSERT INTO public.\"{tableName}\" ({colNames}) VALUES ({colVals}) ON CONFLICT DO NOTHING";

        int inserted = 0;
        foreach (var row in usedRows.Skip(1))
        {
            var cells = row.Cells(1, headers.Count).ToList();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < headers.Count; i++)
            {
                var strVal = cells.Count > i ? cells[i].GetString().Trim() : "";
                cmd.Parameters.AddWithValue($"p{i}",
                    string.IsNullOrEmpty(strVal) ? DBNull.Value : (object)strVal);
            }
            try { await cmd.ExecuteNonQueryAsync(); inserted++; }
            catch { /* 행 오류 건너뜀 */ }
        }
        return inserted;
    }

    private static async Task<Dictionary<string, string>> GetColumnTypesAsync(
        NpgsqlConnection conn, string tableName)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @t
            ORDER BY ordinal_position
            """;
        cmd.Parameters.AddWithValue("t", tableName);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            dict[r.GetString(0)] = r.GetString(1);
        return dict;
    }

    private static string MapCastType(string pgType) => pgType switch
    {
        "smallint"                    => "smallint",
        "integer"                     => "integer",
        "bigint"                      => "bigint",
        "real"                        => "real",
        "double precision"            => "double precision",
        "numeric"                     => "numeric",
        "boolean"                     => "boolean",
        "timestamp without time zone" => "timestamp",
        "timestamp with time zone"    => "timestamptz",
        "date"                        => "date",
        _                             => "text"
    };
}
