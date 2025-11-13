using RabbitMQ.Client.Events;

namespace SharedModels;

public static class RabbitMqTracingExtensions
{
    private const string TraceparentHeaderName = "traceparent";

    public static void InjectTraceContext(this IBasicProperties properties)
    {
        if (Activity.Current != null)
        {
            properties.Headers ??= new Dictionary<string, object?>();
            properties.Headers[TraceparentHeaderName] = Encoding.UTF8.GetBytes(Activity.Current.Id ?? "");
        }
    }

    public static Activity? StartActivityFromMessage(
        this ActivitySource activitySource,
        string activityName,
        BasicDeliverEventArgs eventArgs)
    {
        ActivityContext parentContext = default;

        if (eventArgs.BasicProperties?.Headers?.TryGetValue(TraceparentHeaderName, out var tp) == true)
        {
            var traceParent = Encoding.UTF8.GetString((byte[])tp);
            if (ActivityContext.TryParse(traceParent, null, out var parsedContext))
            {
                parentContext = parsedContext;
            }
        }

        var activity = parentContext != default
            ? activitySource.StartActivity(activityName, ActivityKind.Consumer, parentContext)
            : activitySource.StartActivity(activityName, ActivityKind.Consumer);

        if (activity != null)
        {
            activity.SetTag("messaging.system", "rabbitmq");
            activity.SetTag("messaging.operation", "receive");
            activity.SetTag("messaging.destination", eventArgs.Exchange);
            activity.SetTag("messaging.routing_key", eventArgs.RoutingKey);
        }

        return activity;
    }

    public static void RecordError(this Activity? activity, Exception exception)
    {
        if (activity != null)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.RecordException(exception);
        }
    }
}