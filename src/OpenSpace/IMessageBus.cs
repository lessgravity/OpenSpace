using System;
using System.Threading.Tasks;

namespace OpenSpace;

public interface IMessageBus
{
    void Unsubscribe(MessageBus.SubscriptionToken subscriptionToken);

    Task PublishWaitAllAsync<TMessage>(TMessage message);

    Task PublishWaitAsync<TMessage>(TMessage message);

    void PublishWait<TMessage>(TMessage message);

    MessageBus.SubscriptionToken Subscribe<TMessage>(Func<TMessage, Task> subscriber);
}