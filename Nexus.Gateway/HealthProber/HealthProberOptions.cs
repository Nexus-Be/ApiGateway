namespace Nexus.Gateway.HealthProber;

public sealed class HealthProberOptions
{
    public const string SectionName = "Gateway:HealthProber";

    /// <summary>
    /// Chu kỳ gõ cửa kiểm tra (mặc định 60 giây = 1 phút)
    /// </summary>
    public int IntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Thời gian chờ phản hồi tối đa (mặc định 30 giây)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Số lần lỗi liên tiếp trước khi kết luận Unhealthy (mặc định 5 lần)
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Thời gian ân hạn khi container mới bật (mặc định 60 giây = 1 phút)
    /// </summary>
    public int StartPeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Tự động xóa và tái tạo (recreate) container khi Unhealthy
    /// </summary>
    public bool AutoRecoverContainer { get; set; } = true;

    /// <summary>
    /// Giới hạn số lần tự động hồi phục liên tiếp để tránh CrashLoopBackOff (mặc định 3 lần)
    /// </summary>
    public int MaxAutoRecoverAttempts { get; set; } = 3;

    /// <summary>
    /// Thư mục lưu trữ log sự cố (Incident log) tại Gateway
    /// </summary>
    public string LogDirectory { get; set; } = "logs/health";

    /// <summary>
    /// Giới hạn số dòng tối đa trên 1 tệp log trước khi xoay sang tệp mới (mặc định 1000 dòng)
    /// </summary>
    public int MaxLinesPerLogFile { get; set; } = 1000;
}
