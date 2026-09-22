using System.Collections.Generic;

namespace AITool.AIProviders
{
    /// <summary>Maps a URLTypeEnum to the provider that knows how to talk to it.</summary>
    public static class AIProviderRegistry
    {
        private static readonly IAIProvider DeepStackCompatible = new DeepStackCompatibleProvider();
        private static readonly IAIProvider SightHound = new SightHoundProvider();
        private static readonly IAIProvider Doods = new DoodsProvider();
        private static readonly IAIProvider AwsRekognition = new AwsRekognitionProvider();
        private static readonly IAIProvider VisionLlm = new VisionLlmProvider();

        private static readonly Dictionary<URLTypeEnum, IAIProvider> Providers = new Dictionary<URLTypeEnum, IAIProvider>
        {
            { URLTypeEnum.CodeProject_AI, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_Faces, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_Custom, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_Scene, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_Plate, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_IPCAM_Animal, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_IPCAM_Dark, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_IPCAM_General, DeepStackCompatible },
            { URLTypeEnum.CodeProject_AI_IPCAM_Combined, DeepStackCompatible },
            { URLTypeEnum.DeepStack, DeepStackCompatible },
            { URLTypeEnum.DeepStack_Faces, DeepStackCompatible },
            { URLTypeEnum.DeepStack_Custom, DeepStackCompatible },
            { URLTypeEnum.DeepStack_Scene, DeepStackCompatible },
            { URLTypeEnum.DOODS, Doods },
            { URLTypeEnum.AWSRekognition_Objects, AwsRekognition },
            { URLTypeEnum.AWSRekognition_Faces, AwsRekognition },
            { URLTypeEnum.SightHound_Vehicle, SightHound },
            { URLTypeEnum.SightHound_Person, SightHound },
            { URLTypeEnum.OpenAI_Vision, VisionLlm },
            { URLTypeEnum.Anthropic_Vision, VisionLlm },
        };

        /// <summary>Returns null for types with no backend implementation (Other, Unknown).</summary>
        public static IAIProvider Get(URLTypeEnum type)
        {
            return Providers.TryGetValue(type, out IAIProvider provider) ? provider : null;
        }
    }
}
