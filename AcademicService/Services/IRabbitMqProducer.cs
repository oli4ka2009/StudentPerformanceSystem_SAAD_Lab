namespace AcademicService.Services
{
    public interface IRabbitMqProducer : IAsyncDisposable
    {
        Task PublishMessageAsync<T>(T message, string exchangeName, string routingKey);
    }
}
