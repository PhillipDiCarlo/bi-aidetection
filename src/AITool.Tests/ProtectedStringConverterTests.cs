using AITool;
using Newtonsoft.Json;
using Xunit;

namespace AITool.Tests;

public class ProtectedStringConverterTests
{
    private class Holder
    {
        [JsonConverter(typeof(ProtectedStringConverter))]
        public string Secret = "";
        public string Plain = "";
    }

    [Fact]
    public void RoundTrip_EncryptsOnDiskAndDecryptsInMemory()
    {
        var json = JsonConvert.SerializeObject(new Holder { Secret = "hunter2", Plain = "visible" });

        Assert.DoesNotContain("hunter2", json);
        Assert.Contains("\"Secret\":\"enc:", json);
        Assert.Contains("\"Plain\":\"visible\"", json);

        var back = JsonConvert.DeserializeObject<Holder>(json);
        Assert.Equal("hunter2", back.Secret);
    }

    [Fact]
    public void LegacyPlaintextIsReadAsIs()
    {
        var back = JsonConvert.DeserializeObject<Holder>("{\"Secret\":\"old-plaintext-token\"}");
        Assert.Equal("old-plaintext-token", back.Secret);
    }

    [Theory]
    [InlineData("{\"Secret\":\"\"}")]
    [InlineData("{\"Secret\":null}")]
    [InlineData("{}")]
    public void EmptyStaysEmpty(string json)
    {
        var back = JsonConvert.DeserializeObject<Holder>(json);
        Assert.Equal("", back.Secret);
        Assert.DoesNotContain("enc:", JsonConvert.SerializeObject(back));
    }

    [Fact]
    public void GarbageAfterPrefixDoesNotThrow()
    {
        var back = JsonConvert.DeserializeObject<Holder>("{\"Secret\":\"enc:not-base64!!\"}");
        Assert.Equal("", back.Secret);
    }
}

public class SettingsSecretsTests
{
    [Fact]
    public void RealSettingsClass_SecretsAreEncryptedWithTheAppSerializerSettings()
    {
        var settings = new AppSettings.ClsSettings
        {
            telegram_token = "123456:legacy-token",
            mqtt_password = "mqtt-secret",
            pushover_APIKey = "po-api",
            pushover_UserKey = "po-user",
            AmazonSecretKey = "aws-secret",
            SightHoundAPIKey = "sh-key",
            deepstack_adminkey = "ds-admin",
            deepstack_apikey = "ds-api",
        };

        string json = Global.GetJSONString(settings);

        foreach (string secret in new[] { "123456:legacy-token", "mqtt-secret", "po-api", "po-user", "aws-secret", "sh-key", "ds-admin", "ds-api" })
            Assert.DoesNotContain(secret, json);

        var back = Global.SetJSONString<AppSettings.ClsSettings>(json);
        Assert.Equal("123456:legacy-token", back.telegram_token);
        Assert.Equal("mqtt-secret", back.mqtt_password);
        Assert.Equal("po-api", back.pushover_APIKey);
        Assert.Equal("po-user", back.pushover_UserKey);
        Assert.Equal("aws-secret", back.AmazonSecretKey);
        Assert.Equal("sh-key", back.SightHoundAPIKey);
        Assert.Equal("ds-admin", back.deepstack_adminkey);
        Assert.Equal("ds-api", back.deepstack_apikey);
    }

    [Fact]
    public void RealSettingsClass_LegacyPlaintextFileStillLoads()
    {
        string legacyJson = "{\"telegram_token\":\"123456:old\",\"mqtt_password\":\"oldpw\",\"BlueIrisServer\":\"10.0.0.5\"}";
        var back = Global.SetJSONString<AppSettings.ClsSettings>(legacyJson);
        Assert.Equal("123456:old", back.telegram_token);
        Assert.Equal("oldpw", back.mqtt_password);
        Assert.Equal("10.0.0.5", back.BlueIrisServer);
    }
}
