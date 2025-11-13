using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCustomOpenTelemetry("ApiGateway");

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddOcelot(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();
app.UseCors("AllowAngularApp");

app.Use(async (context, next) =>
{
    var activitySource = context.RequestServices.GetRequiredService<ActivitySource>();
    using (var activity = activitySource.StartActivity("ApiGateway-Request", ActivityKind.Server))
    {
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.path", context.Request.Path);
        try
        {
            await next(context);
            activity?.SetTag("http.status_code", context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
});

await app.UseOcelot();
app.Run();