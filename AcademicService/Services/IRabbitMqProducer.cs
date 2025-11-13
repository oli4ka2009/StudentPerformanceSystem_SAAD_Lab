namespace AcademicService.Services
{
    public interface IRabbitMqProducer
    {
        Task PublishMessageAsync<T>(T message, string exchangeName, string routingKey);
    }
}
