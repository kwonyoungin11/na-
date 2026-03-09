namespace NaraCollector.Desktop.Models;

public record DashboardFilter(
    DateTime StartDate,
    DateTime EndDate,
    string Region,
    string Keyword,
    string OrderingAgency,
    string DataType,
    string Company,
    double? MinAdjustRate,
    double? MaxAdjustRate);

public record LoadingProgress(
    string Stage,
    int Percent,
    string Message);

public record KpiCardModel(string Title, string Value, string Unit);

public record ChartSectionModel(string Title, string Description);

public record NoticeRowModel(
    string NoticeNumber,
    string NoticeName,
    string OrderingAgency,
    DateTime OpenDate,
    double AdjustRate,
    bool IsAwarded);

public record CollectorNoticeRowModel(
    DateTime NoticeDate,
    string NoticeNumber,
    string NoticeName,
    string OrderingAgency,
    string RegionText,
    string ParticipantCountText);

public record CompanyAggregateRowModel(
    string CompanyName,
    int ParticipationCount,
    int AwardCount,
    double AwardRate,
    double AverageAdjustRate);

public record AdjustRateBinRowModel(
    string RangeLabel,
    int Count,
    double SharePercent);

public record TacticalCompanyRowModel(
    string CompanyName,
    string RelativeRateText,
    string OverlapText,
    string SampleText,
    string ThreatLabel,
    string ThreatScoreText);

public record TacticalSignalRowModel(
    string Label,
    string Value,
    string Detail);

public record MetricBarRowModel(
    string Label,
    string FullLabel,
    double Value,
    string ValueText,
    string Detail);

public record TrendPointModel(
    string Label,
    double Value);

public record HistogramBinModel(
    string Label,
    double Start,
    double End,
    double Value,
    string ValueText);

public record ZoneSegmentModel(
    string Label,
    double Value,
    string ValueText);

public record TacticalNoticeRowModel(
    DateTime OpenDate,
    string NoticeNumber,
    string NoticeName,
    string CloseCompanies,
    int CloseCompanyCount,
    string DonggukAdjustRateText);

public record ParticipantRecord(
    string NoticeNo,
    string NoticeNumber,
    string NoticeName,
    string OrderingAgency,
    string Region,
    int Rank,
    string CompanyName,
    string NormalizedCompanyName,
    DateTime OpenDate,
    DateTime? BidDate,
    long? BidAmount,
    double? BidRateVsEstimate,
    double? BidRateVsBase,
    double? AdjustRate,
    long? EstimatePrice,
    long? LowerLimitPrice,
    long? BaseAmount,
    double? PriceAdjustmentRate,
    double? LowerRate,
    long? LowerDiff,
    string BizNo,
    string CeoName,
    string Remark,
    bool IsAwarded);

public record RankAlertModel(
    string NoticeNumber,
    string NoticeName,
    DateTime OpenDate,
    string CompanyName,
    int Rank,
    double? AdjustRate,
    string Region);

public record DashboardSnapshot(
    IReadOnlyList<KpiCardModel> Kpis,
    IReadOnlyList<NoticeRowModel> NoticeRows,
    string DataSource,
    string SavedFilePath = "",
    string PositionMapPath = "",
    string RecommendationPath = "",
    IReadOnlyList<string>? CompanyOptions = null,
    IReadOnlyList<ParticipantRecord>? Participants = null,
    IReadOnlyList<CollectorNoticeRowModel>? IncrementalBidNoticeRows = null,
    IReadOnlyList<CollectorNoticeRowModel>? IncrementalAwardNoticeRows = null);
