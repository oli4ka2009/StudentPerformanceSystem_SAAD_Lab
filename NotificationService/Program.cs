using NotificationService;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

System.Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

// --- Налаштування OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
        tracerProviderBuilder
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: "NotificationService", serviceVersion: "1.0.0"))
            .AddAspNetCoreInstrumentation() // Трасування вхідних HTTP
            .AddSource("RabbitMQ.Client") // Трасування отримання з RabbitMQ
            .AddJaegerExporter(o =>
            {
                o.AgentHost = "localhost";
                o.AgentPort = 6831;
            }));
// ------------------------------------

// Додаємо Worker як Hosted Service
builder.Services.AddHostedService<Worker>();

// Додаємо Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();