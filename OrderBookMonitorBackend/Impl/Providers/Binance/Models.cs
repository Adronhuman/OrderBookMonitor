﻿#nullable disable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrderBookMonitorBackend.Impl.Providers.Binance
{
    public class OrderBook
    {
        public long LastUpdateId { get; set; }

        public List<Order> Bids { get; set; }

        public List<Order> Asks { get; set; }
    }

    public class Order
    {
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
    }

    public class UpdateStreamEvent
    {
        [JsonPropertyName("stream")]
        public string Stream { get; set; }

        [JsonPropertyName("data")]
        public Changes Data { get; set; }
    }

    public class Changes
    {
        [JsonPropertyName("e")]
        public string EventType { get; set; }

        [JsonPropertyName("E")]
        public long EventTime { get; set; }

        [JsonPropertyName("s")]
        public string Symbol { get; set; }

        [JsonPropertyName("U")]
        public long FirstUpdateId { get; set; }

        [JsonPropertyName("u")]
        public long LastUpdateId { get; set; }

        [JsonPropertyName("b")]
        public List<Order> Bids { get; set; }


        [JsonPropertyName("a")]
        public List<Order> Asks { get; set; }
    }

    public class OrderConverter : JsonConverter<Order>
    {
        public override Order Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                reader.Skip();
            }

            reader.Read();
            if (!decimal.TryParse(reader.GetString(), out var price))
            {
                throw new JsonException("Invalid token or missing price value.");
            }

            reader.Read();
            if (!decimal.TryParse(reader.GetString(), out var quantity))
            {
                throw new JsonException("Invalid token or missing price value.");
            }

            reader.Read();

            return new Order { Price = price, Quantity = quantity };
        }

        public override void Write(Utf8JsonWriter writer, Order value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(value.Price.ToString());
            writer.WriteStringValue(value.Quantity.ToString());
            writer.WriteEndArray();
        }
    }
}
