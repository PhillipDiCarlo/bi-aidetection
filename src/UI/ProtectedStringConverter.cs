using System;

using Newtonsoft.Json;

namespace AITool
{
    /// <summary>
    /// Stores a string field encrypted with DPAPI (current user) in the settings JSON.
    /// Encrypted values are written as "enc:&lt;base64&gt;". A value without the prefix is treated as
    /// legacy plaintext and read as-is, so existing settings files keep working and are migrated
    /// to the encrypted form the next time settings are saved.
    /// </summary>
    public class ProtectedStringConverter : JsonConverter<string>
    {
        public const string Prefix = "enc:";

        public override void WriteJson(JsonWriter writer, string value, JsonSerializer serializer)
        {
            if (string.IsNullOrEmpty(value))
                writer.WriteValue(value ?? "");
            else
                writer.WriteValue(Prefix + value.Encrypt());
        }

        public override string ReadJson(JsonReader reader, Type objectType, string existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string raw = reader.Value as string;

            if (string.IsNullOrEmpty(raw))
                return "";

            if (!raw.StartsWith(Prefix, StringComparison.Ordinal))
                return raw;

            // Decrypt() returns "" if the blob was written by a different Windows user/machine
            return raw.Substring(Prefix.Length).Decrypt();
        }
    }
}
