using AITool;
using AITool.AIProviders;
using Xunit;

namespace AITool.Tests;

public class AIProviderRegistryTests
{
    [Theory]
    [InlineData(URLTypeEnum.CodeProject_AI, typeof(DeepStackCompatibleProvider))]
    [InlineData(URLTypeEnum.CodeProject_AI_IPCAM_Combined, typeof(DeepStackCompatibleProvider))]
    [InlineData(URLTypeEnum.DeepStack, typeof(DeepStackCompatibleProvider))]
    [InlineData(URLTypeEnum.DeepStack_Faces, typeof(DeepStackCompatibleProvider))]
    [InlineData(URLTypeEnum.DOODS, typeof(DoodsProvider))]
    [InlineData(URLTypeEnum.SightHound_Person, typeof(SightHoundProvider))]
    [InlineData(URLTypeEnum.SightHound_Vehicle, typeof(SightHoundProvider))]
    [InlineData(URLTypeEnum.AWSRekognition_Objects, typeof(AwsRekognitionProvider))]
    [InlineData(URLTypeEnum.AWSRekognition_Faces, typeof(AwsRekognitionProvider))]
    public void Get_ReturnsProviderForEveryImplementedType(URLTypeEnum type, System.Type expected)
    {
        Assert.IsType(expected, AIProviderRegistry.Get(type));
    }

    [Theory]
    [InlineData(URLTypeEnum.Other)]
    [InlineData(URLTypeEnum.Unknown)]
    public void Get_ReturnsNullForUnimplementedTypes(URLTypeEnum type)
    {
        Assert.Null(AIProviderRegistry.Get(type));
    }

    [Fact]
    public void EveryUrlTypeExceptOtherAndUnknownHasAProvider()
    {
        foreach (URLTypeEnum type in System.Enum.GetValues<URLTypeEnum>())
        {
            if (type == URLTypeEnum.Other || type == URLTypeEnum.Unknown)
                continue;
            Assert.NotNull(AIProviderRegistry.Get(type));
        }
    }
}
