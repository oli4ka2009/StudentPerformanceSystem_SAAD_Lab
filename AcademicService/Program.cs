using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AcademicService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;

            var builder = WebApplication.CreateBuilder(args);

            // --- Налаштування OpenTelemetry ---
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                    tracerProviderBuilder
                        .SetResourceBuilder(
                            ResourceBuilder.CreateDefault()
                                .AddService(serviceName: "AcademicService", serviceVersion: "1.0.0"))
                        .AddAspNetCoreInstrumentation() // Трасування вхідних HTTP
                        .AddHttpClientInstrumentation() // Трасування вихідних HTTP-дзвінків
                        .AddSource("RabbitMQ.Client") // Трасування публікації в RabbitMQ
                        .AddJaegerExporter(o =>
                        {
                            o.AgentHost = "localhost";
                            o.AgentPort = 6831;
                        }));

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<AcademicService.Services.IRabbitMqProducer, AcademicService.Services.RabbitMqProducer>();
            builder.Services.AddHttpClient();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
