﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lib.extension;
using Lib.helper;
using Lib.io;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Polly;
using Lib.cache;
using Lib.data;

namespace Lib.mq
{
    public abstract class RabbitMessageConsumerBase<DeliveryDataType> : IMessageQueueConsumer<DeliveryDataType>
    {
        private readonly IModel _channel;
        private readonly EventingBasicConsumer _consumer;
        private readonly string _exchange_name;
        private readonly string _queue_name;
        private readonly string _route_key;
        private readonly string _consumer_name;
        private readonly bool _ack;

        private readonly SerializeHelper _serializer = new SerializeHelper();

        public RabbitMessageConsumerBase(IModel channel, string consumer_name,
            string exchange_name, string queue_name, string route_key, ExchangeTypeEnum exchangeType,
            bool persist, bool ack, ushort concurrency, bool delay)
        {
            this._channel = channel ?? throw new ArgumentNullException(nameof(channel));
            this._exchange_name = exchange_name;
            this._queue_name = queue_name;
            this._route_key = route_key;
            this._consumer_name = consumer_name;
            this._ack = ack;

            //exchange
            this._channel.X_ExchangeDeclare(exchange_name, exchangeType, durable: persist, auto_delete: !ack, is_delay: delay);
            //queue
            var queue_args = new Dictionary<string, object>() { };
            this._channel.QueueDeclare(queue_name, durable: persist, exclusive: false, autoDelete: !ack, arguments: queue_args);
            //bind
            this._channel.QueueBind(queue_name, exchange_name, route_key);
            //qos
            this._channel.BasicQos(0, concurrency, false);

            //consumer
            this._consumer = new EventingBasicConsumer(this._channel);
            this._consumer.Received += async (sender, args) =>
            {
                try
                {
                    var message = this._serializer.Deserialize<DeliveryDataType>(args.Body);
                    var result = await this.OnMessageReceived(message, sender, args);
                    if (this._ack && result != null && result.Value)
                    {
                        this._channel.X_BasicAck(args);
                    }
                }
                catch (Exception e)
                {
                    //log errors
                    e.AddErrorLog($"无法消费");
                }
            };
            var consumerTag = $"{Environment.MachineName}|{this._queue_name}|{this._consumer_name}";
            this._channel.BasicConsume(
                queue: queue_name,
                noAck: !this._ack,
                consumerTag: consumerTag,
                consumer: this._consumer);
        }

        public abstract Task<bool?> OnMessageReceived(DeliveryDataType message, object sender, BasicDeliverEventArgs args);

        public void Dispose()
        {
            try
            {
                this._channel?.Close();
                this._channel?.Dispose();
            }
            catch (Exception e)
            {
                e.AddErrorLog();
            }
        }
    }
}
