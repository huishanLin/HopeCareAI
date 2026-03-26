using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedClassLibrary
{
    public class CustomDateTimeConverter : JsonConverter<DateTime>
    {
        private readonly string _format = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTime.Parse(reader.GetString());
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // 關鍵步驟：在轉換為字串前，呼叫 .ToLocalTime()
            // 這會根據執行此程式碼的伺服器時區進行轉換
            DateTime localValue = value.ToLocalTime();
            writer.WriteStringValue(localValue.ToString(_format));
        }
    }
}
