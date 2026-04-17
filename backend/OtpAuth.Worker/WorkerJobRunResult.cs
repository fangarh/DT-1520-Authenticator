namespace OtpAuth.Worker;

public sealed record WorkerJobRunResult(
    string Summary,
    IReadOnlyList<WorkerJobMetricSnapshot> Metrics)
{
    public static WorkerJobRunResult Create(
        string summary,
        params WorkerJobMetricSnapshot[] metrics)
    {
        return new WorkerJobRunResult(summary, metrics);
    }
}
