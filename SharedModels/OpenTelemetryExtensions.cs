// ОНОВЛЕНО: Ми прибрали все ручне трасування.
// Ми залишили ТІЛЬКИ автоматичну інструментацію для
// ASP.NET (вхідні) та HttpClient (вихідні) запити.

using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddCustomOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        // Ми більше не реєструємо кастомний ActivitySource
        // services.AddSingleton(new ActivitySource(serviceName));
        var activitySource = new ActivitySource(serviceName);
        services.AddOpenTelemetry()
            .WithTracing(tracerProviderBuilder =>
                tracerProviderBuilder
                    .AddSource(serviceName)
                    .AddSource("Microsoft.AspNetCore") // Авто-трасування ASP.NET
                    .AddSource("System.Net.Http")
                    .AddSource("RabbitMQ.Client")
                    .SetResourceBuilder(
                        ResourceBuilder.CreateDefault()
                            .AddService(serviceName, serviceVersion))
                    .SetSampler(new AlwaysOnSampler())
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation(options => // ‼️ ЦЕ ВАЖЛИВО ДЛЯ ОРКЕСТРАЦІЇ
                    {
                        options.RecordException = true;
                    })
                    .AddConsoleExporter()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri("http://localhost:4317");
                        options.Protocol = OtlpExportProtocol.Grpc;
                    }));

        return services;
    }
}