using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ.Consumer;
using EasyNetQ.Events;
using EasyNetQ.Loggers;
using EasyNetQ.Tests.Mocking;
using EasyNetQ.Topology;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Client.Framing.v0_9_1;
using Rhino.Mocks;

namespace EasyNetQ.Tests.ConsumeTests
{
    public abstract class ConsumerTestBase
    {
        protected MockBuilder MockBuilder;
        protected IConsumerErrorStrategy ConsumerErrorStrategy;
        protected const string ConsumerTag = "the_consumer_tag";
        protected byte[] DeliveredMessageBody;
        protected MessageProperties DeliveredMessageProperties;
        protected MessageReceivedInfo DeliveredMessageInfo;
        protected bool ConsumerWasInvoked;

        // populated when a message is delivered
        protected IBasicProperties OriginalProperties;
        protected byte[] OriginalBody;
        protected const ulong DeliverTag = 10101;

        [SetUp]
        protected void SetUp()
        {
            ConsumerErrorStrategy = MockRepository.GenerateStub<IConsumerErrorStrategy>();

            IConventions conventions = new Conventions(new TypeNameSerializer())
                {
                    ConsumerTagConvention = () => ConsumerTag
                };
            MockBuilder = new MockBuilder(x => x
                    .Register(_ => conventions)
                    .Register(_ => ConsumerErrorStrategy)
                    //.Register<IEasyNetQLogger>(_ => new ConsoleLogger())
                );

            AdditionalSetUp();
        }

        protected abstract void AdditionalSetUp();

        protected void StartConsumer(Action<byte[], MessageProperties, MessageReceivedInfo> handler)
        {
            ConsumerWasInvoked = false;
            var queue = new Queue("my_queue", false);
            MockBuilder.Bus.Advanced.Consume(queue, (body, properties, messageInfo) => Task.Factory.StartNew(() =>
                {
                    DeliveredMessageBody = body;
                    DeliveredMessageProperties = properties;
                    DeliveredMessageInfo = messageInfo;

                    handler(body, properties, messageInfo);
                    ConsumerWasInvoked = true;
                }));
        }

        protected void DeliverMessage()
        {
            OriginalProperties = new BasicProperties
                {
                    Type = "the_message_type",
                    CorrelationId = "the_correlation_id"
                };
            OriginalBody = Encoding.UTF8.GetBytes("Hello World");

            MockBuilder.Consumers[0].HandleBasicDeliver(
                ConsumerTag,
                DeliverTag,
                false,
                "the_exchange",
                "the_routing_key",
                OriginalProperties,
                OriginalBody
                );

            WaitForMessageDispatchToComplete();
        }

        protected void WaitForMessageDispatchToComplete()
        {
            // wait for the subscription thread to handle the message ...
            var autoResetEvent = new AutoResetEvent(false);
            MockBuilder.EventBus.Subscribe<AckEvent>(x => autoResetEvent.Set());
            autoResetEvent.WaitOne(1000);
        }
    }
}