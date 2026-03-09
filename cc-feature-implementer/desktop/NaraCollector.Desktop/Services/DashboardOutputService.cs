using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using NaraCollector.Desktop.Models;

namespace NaraCollector.Desktop.Services;

public sealed class DashboardOutputService
{
    private const int SqliteBusyTimeoutMs = 5000;
    private const int SqliteCacheSizeKb = 32768;

    public string AppRootDirectory { get; }
    public string DatabasePath => Path.Combine(AppRootDirectory, "nara-dashboard.db");
    public string ExportDirectory => Path.Combine(AppRootDirectory, "exports");
    public string ChartDirectory => Path.Combine(AppRootDirectory, "charts");
    public string MasterExcelPath { get; }

    public DashboardOutputService(string? appRootDirectory = null)
    {
        AppRootDirectory = appRootDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NaraCollector");
        MasterExcelPath = ResolveMasterExcelPath(AppRootDirectory);
    }

    public async Task<SaveResult> SaveAllAsync(
        IReadOnlyList<ParticipantRecord> participants,
        DashboardFilter filter,
        IReadOnlyList<ParticipantRecord>? chartParticipants = null,
        IProgress<LoadingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppRootDirectory);
        Directory.CreateDirectory(ExportDirectory);
        Directory.CreateDirectory(ChartDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var selectedForExport = chartParticipants is { Count: > 0 } ? chartParticipants : participants;
        var regionToken = NormalizeFileToken(filter.Region, "전국");
        var dataTypeToken = NormalizeFileToken(filter.DataType, "전체");

        progress?.Report(new LoadingProgress("save", 82, "SQLite 저장 중"));
        await SaveParticipantsToSqliteAsync(participants, cancellationToken);

        progress?.Report(new LoadingProgress("export", 87, "전처리 결과 CSV 파일 생성 중"));
        WriteCsvFile(
            selectedForExport,
            Path.Combine(ExportDirectory, $"나라장터_전처리완료_{regionToken}_{dataTypeToken}_{timestamp}.csv"));
        WriteCsvFile(
            participants,
            Path.Combine(ExportDirectory, $"나라장터_전처리완료_원천전체_{timestamp}.csv"));
        WritePartitionCsvFiles(participants, timestamp);

        progress?.Report(new LoadingProgress("export", 90, "마스터 엑셀 저장 중"));
        var masterPath = WriteMasterExcelFile(participants, MasterExcelPath);

        progress?.Report(new LoadingProgress("chart", 93, "네이티브 렌더러 모드: 파일 기반 HTML 차트 생성을 건너뜁니다."));
        return new SaveResult(masterPath, string.Empty, string.Empty);
    }

    public (string PositionMapPath, string RecommendationPath) SaveChartsOnly(
        IReadOnlyList<ParticipantRecord>? participants,
        DashboardFilter? filter = null)
    {
        return (string.Empty, string.Empty);
    }

    public Task ReplaceParticipantsInDatabaseAsync(
        IReadOnlyList<ParticipantRecord> participants,
        CancellationToken cancellationToken = default)
    {
        return SaveParticipantsToSqliteAsync(participants, cancellationToken);
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppRootDirectory);
        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, writeOptimized: true, cancellationToken);

        var sql = """
                  CREATE TABLE IF NOT EXISTS participants (
                      notice_no TEXT NOT NULL,
                      notice_number TEXT NOT NULL,
                      notice_name TEXT NOT NULL,
                      ordering_agency TEXT NOT NULL,
                      region TEXT NOT NULL,
                      rank INTEGER NOT NULL,
                      company_name TEXT NOT NULL,
                      normalized_company_name TEXT NOT NULL,
                      open_date TEXT NOT NULL,
                      bid_date TEXT NULL,
                      bid_amount INTEGER NULL,
                      bid_rate_vs_estimate REAL NULL,
                      bid_rate_vs_base REAL NULL,
                      adjust_rate REAL NULL,
                      estimate_price INTEGER NULL,
                      lower_limit_price INTEGER NULL,
                      base_amount INTEGER NULL,
                      price_adjustment_rate REAL NULL,
                      lower_rate REAL NULL,
                      lower_diff INTEGER NULL,
                      biz_no TEXT NULL,
                      ceo_name TEXT NULL,
                      remark TEXT NULL,
                      is_awarded INTEGER NOT NULL DEFAULT 0,
                      updated_at TEXT NOT NULL
                  );
                  CREATE INDEX IF NOT EXISTS ix_participants_open_date ON participants(open_date DESC);
                  CREATE INDEX IF NOT EXISTS ix_participants_notice_no ON participants(notice_no);
                  CREATE INDEX IF NOT EXISTS ix_participants_notice_number ON participants(notice_number);
                  CREATE INDEX IF NOT EXISTS ix_participants_region ON participants(region);
                  CREATE INDEX IF NOT EXISTS ix_participants_company_name ON participants(normalized_company_name);
                  CREATE INDEX IF NOT EXISTS ix_participants_awarded_open_date ON participants(is_awarded, open_date DESC);
                  CREATE INDEX IF NOT EXISTS ix_participants_adjust_rate ON participants(adjust_rate);
                  CREATE INDEX IF NOT EXISTS ix_participants_open_notice_rank ON participants(open_date DESC, notice_no, rank ASC);
                  CREATE INDEX IF NOT EXISTS ix_participants_notice_rank_award_open ON participants(notice_number, rank ASC, is_awarded, open_date DESC);
                  CREATE INDEX IF NOT EXISTS ix_participants_notice_key_awarded ON participants(
                      CASE
                          WHEN TRIM(IFNULL(notice_number, '')) <> '' THEN notice_number
                          ELSE notice_no
                      END,
                      is_awarded
                  );
                  """;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task SaveParticipantsToSqliteAsync(
        IReadOnlyList<ParticipantRecord> participants,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);

        await using var connection = new SqliteConnection($"Data Source={DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        await ConfigureConnectionAsync(connection, writeOptimized: true, cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.Transaction = transaction;
            deleteCmd.CommandText = "DELETE FROM participants;";
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
                                 INSERT INTO participants (
                                     notice_no, notice_number, notice_name, ordering_agency, region, rank,
                                     company_name, normalized_company_name, open_date, bid_date,
                                     bid_amount, bid_rate_vs_estimate, bid_rate_vs_base, adjust_rate,
                                     estimate_price, lower_limit_price, base_amount, price_adjustment_rate,
                                     lower_rate, lower_diff, biz_no, ceo_name, remark, is_awarded, updated_at
                                 ) VALUES (
                                     $notice_no, $notice_number, $notice_name, $ordering_agency, $region, $rank,
                                     $company_name, $normalized_company_name, $open_date, $bid_date,
                                     $bid_amount, $bid_rate_vs_estimate, $bid_rate_vs_base, $adjust_rate,
                                     $estimate_price, $lower_limit_price, $base_amount, $price_adjustment_rate,
                                     $lower_rate, $lower_diff, $biz_no, $ceo_name, $remark, $is_awarded, $updated_at
                                 );
                                 """;

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = insertSql;

        var pNoticeNo = cmd.Parameters.Add("$notice_no", SqliteType.Text);
        var pNoticeNumber = cmd.Parameters.Add("$notice_number", SqliteType.Text);
        var pNoticeName = cmd.Parameters.Add("$notice_name", SqliteType.Text);
        var pOrderingAgency = cmd.Parameters.Add("$ordering_agency", SqliteType.Text);
        var pRegion = cmd.Parameters.Add("$region", SqliteType.Text);
        var pRank = cmd.Parameters.Add("$rank", SqliteType.Integer);
        var pCompanyName = cmd.Parameters.Add("$company_name", SqliteType.Text);
        var pNormalizedCompanyName = cmd.Parameters.Add("$normalized_company_name", SqliteType.Text);
        var pOpenDate = cmd.Parameters.Add("$open_date", SqliteType.Text);
        var pBidDate = cmd.Parameters.Add("$bid_date", SqliteType.Text);
        var pBidAmount = cmd.Parameters.Add("$bid_amount", SqliteType.Integer);
        var pBidRateVsEstimate = cmd.Parameters.Add("$bid_rate_vs_estimate", SqliteType.Real);
        var pBidRateVsBase = cmd.Parameters.Add("$bid_rate_vs_base", SqliteType.Real);
        var pAdjustRate = cmd.Parameters.Add("$adjust_rate", SqliteType.Real);
        var pEstimatePrice = cmd.Parameters.Add("$estimate_price", SqliteType.Integer);
        var pLowerLimitPrice = cmd.Parameters.Add("$lower_limit_price", SqliteType.Integer);
        var pBaseAmount = cmd.Parameters.Add("$base_amount", SqliteType.Integer);
        var pPriceAdjustmentRate = cmd.Parameters.Add("$price_adjustment_rate", SqliteType.Real);
        var pLowerRate = cmd.Parameters.Add("$lower_rate", SqliteType.Real);
        var pLowerDiff = cmd.Parameters.Add("$lower_diff", SqliteType.Integer);
        var pBizNo = cmd.Parameters.Add("$biz_no", SqliteType.Text);
        var pCeoName = cmd.Parameters.Add("$ceo_name", SqliteType.Text);
        var pRemark = cmd.Parameters.Add("$remark", SqliteType.Text);
        var pIsAwarded = cmd.Parameters.Add("$is_awarded", SqliteType.Integer);
        var pUpdatedAt = cmd.Parameters.Add("$updated_at", SqliteType.Text);

        cmd.Prepare();

        var nowText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        foreach (var row in participants)
        {
            pNoticeNo.Value = row.NoticeNo;
            pNoticeNumber.Value = row.NoticeNumber;
            pNoticeName.Value = row.NoticeName;
            pOrderingAgency.Value = row.OrderingAgency;
            pRegion.Value = row.Region;
            pRank.Value = row.Rank;
            pCompanyName.Value = row.CompanyName;
            pNormalizedCompanyName.Value = row.NormalizedCompanyName;
            pOpenDate.Value = row.OpenDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            pBidDate.Value = row.BidDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? (object)DBNull.Value;
            pBidAmount.Value = row.BidAmount ?? (object)DBNull.Value;
            pBidRateVsEstimate.Value = row.BidRateVsEstimate ?? (object)DBNull.Value;
            pBidRateVsBase.Value = row.BidRateVsBase ?? (object)DBNull.Value;
            pAdjustRate.Value = row.AdjustRate ?? (object)DBNull.Value;
            pEstimatePrice.Value = row.EstimatePrice ?? (object)DBNull.Value;
            pLowerLimitPrice.Value = row.LowerLimitPrice ?? (object)DBNull.Value;
            pBaseAmount.Value = row.BaseAmount ?? (object)DBNull.Value;
            pPriceAdjustmentRate.Value = row.PriceAdjustmentRate ?? (object)DBNull.Value;
            pLowerRate.Value = row.LowerRate ?? (object)DBNull.Value;
            pLowerDiff.Value = row.LowerDiff ?? (object)DBNull.Value;
            pBizNo.Value = row.BizNo ?? string.Empty;
            pCeoName.Value = row.CeoName ?? string.Empty;
            pRemark.Value = row.Remark ?? string.Empty;
            pIsAwarded.Value = row.IsAwarded ? 1 : 0;
            pUpdatedAt.Value = nowText;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await OptimizeQueryPlannerAsync(connection, cancellationToken);
    }

    private static async Task ConfigureConnectionAsync(
        SqliteConnection connection,
        bool writeOptimized,
        CancellationToken cancellationToken)
    {
        await ExecutePragmaAsync(connection, $"PRAGMA busy_timeout={SqliteBusyTimeoutMs};", cancellationToken);
        await ExecutePragmaAsync(connection, $"PRAGMA cache_size=-{SqliteCacheSizeKb};", cancellationToken);
        await ExecutePragmaAsync(connection, "PRAGMA temp_store=MEMORY;", cancellationToken);

        if (!writeOptimized)
        {
            return;
        }

        await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecutePragmaAsync(connection, "PRAGMA synchronous=NORMAL;", cancellationToken);
        await ExecutePragmaAsync(connection, "PRAGMA wal_autocheckpoint=1000;", cancellationToken);
    }

    private static async Task ExecutePragmaAsync(
        SqliteConnection connection,
        string pragmaSql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = pragmaSql;
        _ = await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task OptimizeQueryPlannerAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var analyzeCommand = connection.CreateCommand();
            analyzeCommand.CommandText = "ANALYZE participants;";
            await analyzeCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var optimizeCommand = connection.CreateCommand();
            optimizeCommand.CommandText = "PRAGMA optimize;";
            await optimizeCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            // 통계/쿼리플랜 최적화는 실패해도 저장 흐름을 중단하지 않는다.
        }
    }

    private void WritePartitionCsvFiles(IReadOnlyList<ParticipantRecord> participants, string timestamp)
    {
        var partitionDirectory = Path.Combine(ExportDirectory, "분류");
        Directory.CreateDirectory(partitionDirectory);

        var regionFilters = new Dictionary<string, Func<ParticipantRecord, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["전국"] = p => ContainsRegionToken(p.Region, "전국"),
            ["대구"] = p => ContainsRegionToken(p.Region, "대구"),
            ["경북"] = p => ContainsRegionToken(p.Region, "경북"),
        };

        var dataTypeFilters = new Dictionary<string, Func<ParticipantRecord, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["전체"] = _ => true,
            ["입찰"] = p => !p.IsAwarded,
            ["낙찰"] = p => p.IsAwarded,
        };

        foreach (var region in regionFilters)
        {
            foreach (var dataType in dataTypeFilters)
            {
                var subset = participants
                    .Where(p => region.Value(p) && dataType.Value(p))
                    .ToList();

                var path = Path.Combine(
                    partitionDirectory,
                    $"나라장터_전처리완료_{region.Key}_{dataType.Key}_{timestamp}.csv");

                WriteCsvFile(subset, path);
            }
        }
    }

    private static string WriteCsvFile(IReadOnlyList<ParticipantRecord> participants, string outputPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("공고번호,공고명,발주처,지역제한,순위,업체명,개찰일시,투찰일시,투찰금액,예가대비투찰률,기초대비투찰률,기초대비사정률,예정가격,낙찰하한가,기초금액,사정률,발주처투찰률,낙찰하한가차이,사업자번호,대표자,비고");

        foreach (var row in participants)
        {
            var values = new[]
            {
                Csv(row.NoticeNumber),
                Csv(row.NoticeName),
                Csv(row.OrderingAgency),
                Csv(row.Region),
                Csv(row.Rank),
                Csv(row.CompanyName),
                Csv(row.OpenDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                Csv(row.BidDate?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty),
                Csv(row.BidAmount),
                Csv(row.BidRateVsEstimate, "F3"),
                Csv(row.BidRateVsBase, "F3"),
                Csv(row.AdjustRate, "F5"),
                Csv(row.EstimatePrice),
                Csv(row.LowerLimitPrice),
                Csv(row.BaseAmount),
                Csv(row.PriceAdjustmentRate, "F5"),
                Csv(row.LowerRate, "F3"),
                Csv(row.LowerDiff),
                Csv(row.BizNo),
                Csv(row.CeoName),
                Csv(row.Remark),
            };

            sb.AppendLine(string.Join(",", values));
        }

        File.WriteAllText(outputPath, "\uFEFF" + sb, Encoding.UTF8);
        return outputPath;
    }

    private static string WriteMasterExcelFile(IReadOnlyList<ParticipantRecord> participants, string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var ordered = participants
            .OrderBy(x => x.OpenDate)
            .ThenBy(x => x.NoticeNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Rank > 0 ? x.Rank : int.MaxValue)
            .ThenBy(x => x.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("통합데이터");

        var headers = new[]
        {
            "공고번호",
            "공고명",
            "발주처",
            "지역제한",
            "순위",
            "업체명",
            "개찰일시",
            "투찰일시",
            "투찰금액",
            "예가대비투찰률",
            "기초대비투찰률",
            "기초대비사정률",
            "예정가격",
            "낙찰하한가",
            "기초금액",
            "사정률",
            "발주처투찰률",
            "낙찰하한가차이",
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in ordered)
        {
            ws.Cell(rowIndex, 1).Value = row.NoticeNumber;
            ws.Cell(rowIndex, 2).Value = row.NoticeName;
            ws.Cell(rowIndex, 3).Value = row.OrderingAgency;
            ws.Cell(rowIndex, 4).Value = row.Region;
            ws.Cell(rowIndex, 5).Value = row.Rank;
            ws.Cell(rowIndex, 6).Value = row.CompanyName;

            SetDateOrBlank(ws.Cell(rowIndex, 7), row.OpenDate);
            SetDateOrBlank(ws.Cell(rowIndex, 8), row.BidDate);

            SetNumberOrBlank(ws.Cell(rowIndex, 9), row.BidAmount);
            SetNumberOrBlank(ws.Cell(rowIndex, 10), row.BidRateVsEstimate, "0.000");
            SetNumberOrBlank(ws.Cell(rowIndex, 11), row.BidRateVsBase, "0.000");
            SetNumberOrBlank(ws.Cell(rowIndex, 12), row.AdjustRate, "0.00000");
            SetNumberOrBlank(ws.Cell(rowIndex, 13), row.EstimatePrice);
            SetNumberOrBlank(ws.Cell(rowIndex, 14), row.LowerLimitPrice);
            SetNumberOrBlank(ws.Cell(rowIndex, 15), row.BaseAmount);
            SetNumberOrBlank(ws.Cell(rowIndex, 16), row.PriceAdjustmentRate, "0.00000");
            SetNumberOrBlank(ws.Cell(rowIndex, 17), row.LowerRate, "0.000");
            SetNumberOrBlank(ws.Cell(rowIndex, 18), row.LowerDiff);

            rowIndex++;
        }

        ws.Columns().AdjustToContents(1, headers.Length, 8, 52);
        workbook.SaveAs(outputPath);
        return outputPath;
    }

    private static void SetDateOrBlank(IXLCell cell, DateTime value)
    {
        if (value > DateTime.MinValue)
        {
            cell.Value = value;
            cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            return;
        }

        cell.Value = string.Empty;
    }

    private static void SetDateOrBlank(IXLCell cell, DateTime? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            return;
        }

        cell.Value = string.Empty;
    }

    private static void SetNumberOrBlank(IXLCell cell, long? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            return;
        }

        cell.Value = string.Empty;
    }

    private static void SetNumberOrBlank(IXLCell cell, double? value, string format)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            cell.Style.NumberFormat.Format = format;
            return;
        }

        cell.Value = string.Empty;
    }

    private static string ResolveMasterExcelPath(string appRootDirectory)
    {
        var fromEnv = Environment.GetEnvironmentVariable("NARA_MASTER_XLSX");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv.Trim();
        }

        Directory.CreateDirectory(appRootDirectory);
        return Path.Combine(appRootDirectory, "전체_통합_데이터.xlsx");
    }

    private static string NormalizeFileToken(string? raw, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string Csv(string value)
    {
        value ??= string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static string Csv(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Csv(int value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string Csv(double? value, string format)
    {
        return value?.ToString(format, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool ContainsRegionToken(string? sourceRegion, string targetToken)
    {
        if (string.IsNullOrWhiteSpace(sourceRegion) || string.IsNullOrWhiteSpace(targetToken))
        {
            return false;
        }

        var normalizedSource = sourceRegion
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("/", ",", StringComparison.Ordinal)
            .Replace(";", ",", StringComparison.Ordinal)
            .Replace("|", ",", StringComparison.Ordinal)
            .Replace("·", ",", StringComparison.Ordinal)
            .Replace("ㆍ", ",", StringComparison.Ordinal);

        var normalizedTarget = targetToken.Replace(" ", string.Empty, StringComparison.Ordinal);
        var tokens = normalizedSource
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return tokens.Any(token => string.Equals(token, normalizedTarget, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record SaveResult(
    string ExportFilePath,
    string PositionMapPath,
    string RecommendationPath);
