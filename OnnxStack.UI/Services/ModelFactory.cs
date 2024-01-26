﻿using OnnxStack.Core.Config;
using OnnxStack.StableDiffusion.Config;
using OnnxStack.StableDiffusion.Enums;
using OnnxStack.UI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OnnxStack.UI.Services
{
    public class ModelFactory : IModelFactory
    {
        private readonly OnnxStackUIConfig _settings;
        private readonly string _defaultTokenizerPath;

        public ModelFactory(OnnxStackUIConfig settings)
        {
            _settings = settings;
            var defaultTokenizerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cliptokenizer.onnx");
            if (File.Exists(defaultTokenizerPath))
                _defaultTokenizerPath = defaultTokenizerPath;
        }

        public IEnumerable<UpscaleModelTemplate> GetUpscaleModelTemplates()
        {
            yield return new UpscaleModelTemplate("Upscale 2x", 2, 512);
            yield return new UpscaleModelTemplate("Upscale 4x", 4, 512);
        }


        public IEnumerable<StableDiffusionModelTemplate> GetStableDiffusionModelTemplates()
        {
            yield return new StableDiffusionModelTemplate("SD", DiffuserPipelineType.StableDiffusion, ModelType.Base, 512, DiffuserType.TextToImage, DiffuserType.ImageToImage, DiffuserType.ImageInpaintLegacy);
            yield return new StableDiffusionModelTemplate("SD-Inpaint", DiffuserPipelineType.StableDiffusion, ModelType.Base, 512, DiffuserType.ImageInpaint);
            yield return new StableDiffusionModelTemplate("SD-ControlNet", DiffuserPipelineType.StableDiffusion, ModelType.Base, 512, DiffuserType.ControlNet, DiffuserType.ControlNetImage);

            yield return new StableDiffusionModelTemplate("SDXL", DiffuserPipelineType.StableDiffusionXL, ModelType.Base, 1024, DiffuserType.TextToImage, DiffuserType.ImageToImage, DiffuserType.ImageInpaintLegacy);
            yield return new StableDiffusionModelTemplate("SDXL-Inpaint", DiffuserPipelineType.StableDiffusionXL, ModelType.Base, 1024, DiffuserType.ImageInpaint);
            yield return new StableDiffusionModelTemplate("SDXL-Refiner", DiffuserPipelineType.StableDiffusionXL, ModelType.Refiner, 1024, DiffuserType.ImageToImage, DiffuserType.ImageInpaintLegacy);

            yield return new StableDiffusionModelTemplate("LCM", DiffuserPipelineType.LatentConsistency, ModelType.Base, 512, DiffuserType.TextToImage, DiffuserType.ImageToImage, DiffuserType.ImageInpaintLegacy);
            yield return new StableDiffusionModelTemplate("LCM-SDXL", DiffuserPipelineType.LatentConsistencyXL, ModelType.Base, 1024, DiffuserType.TextToImage, DiffuserType.ImageToImage, DiffuserType.ImageInpaintLegacy);

            yield return new StableDiffusionModelTemplate("InstaFlow", DiffuserPipelineType.InstaFlow, ModelType.Base, 512, DiffuserType.TextToImage);
        }


        public StableDiffusionModelSet CreateStableDiffusionModelSet(string name, string folder, StableDiffusionModelTemplate modelTemplate)
        {
            var modelSet = new StableDiffusionModelSet
            {
                Name = name,
                PipelineType = modelTemplate.PipelineType,
                ScaleFactor = 0.18215f,
                TokenizerLimit = 77,
                PadTokenId = 49407,
                TokenizerLength = 768,
                Tokenizer2Length = 1280,
                BlankTokenId = 49407,
                Diffusers = modelTemplate.DiffuserTypes.ToList(),
                SampleSize = modelTemplate.SampleSize,
                TokenizerType = TokenizerType.One,
                ModelType = ModelType.Base,

                DeviceId = _settings.DefaultDeviceId,
                ExecutionMode = _settings.DefaultExecutionMode,
                ExecutionProvider = _settings.DefaultExecutionProvider,
                InterOpNumThreads = _settings.DefaultInterOpNumThreads,
                IntraOpNumThreads = _settings.DefaultIntraOpNumThreads,
                IsEnabled = true,
                ModelConfigurations = new List<OnnxModelConfig>()
            };

            // Some repositories have the ControlNet in the unet folder, some on the controlnet folder
            var isControlNet = modelTemplate.DiffuserTypes.Any(x => x == DiffuserType.ControlNet || x == DiffuserType.ControlNetImage);
            var unetPath = Path.Combine(folder, "unet", "model.onnx");
            var controlNetUnetPath = Path.Combine(folder, "controlnet", "model.onnx");
            if (isControlNet && File.Exists(controlNetUnetPath))
                unetPath = controlNetUnetPath;

            var tokenizerPath = Path.Combine(folder, "tokenizer", "model.onnx");
            var textEncoderPath = Path.Combine(folder, "text_encoder", "model.onnx");
            var vaeDecoder = Path.Combine(folder, "vae_decoder", "model.onnx");
            var vaeEncoder = Path.Combine(folder, "vae_encoder", "model.onnx");
            var tokenizer2Path = Path.Combine(folder, "tokenizer_2", "model.onnx");
            var textEncoder2Path = Path.Combine(folder, "text_encoder_2", "model.onnx");
            var controlnet = Path.Combine(folder, "controlnet", "model.onnx");
            if (!File.Exists(tokenizerPath))
                tokenizerPath = _defaultTokenizerPath;
            if (!File.Exists(tokenizer2Path))
                tokenizer2Path = _defaultTokenizerPath;

            if (modelSet.PipelineType == DiffuserPipelineType.StableDiffusionXL || modelSet.PipelineType == DiffuserPipelineType.LatentConsistencyXL)
            {
                modelSet.PadTokenId = 1;
                modelSet.SampleSize = 1024;
                modelSet.ScaleFactor = 0.13025f;
                modelSet.TokenizerType = TokenizerType.Both;

                if (modelTemplate.ModelType == ModelType.Refiner)
                {
                    modelSet.ModelType = ModelType.Refiner;
                    modelSet.TokenizerType = TokenizerType.Two;
                    modelSet.Diffusers.Remove(DiffuserType.TextToImage);
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Unet, OnnxModelPath = unetPath });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Tokenizer2, OnnxModelPath = tokenizer2Path });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.TextEncoder2, OnnxModelPath = textEncoder2Path });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.VaeDecoder, OnnxModelPath = vaeDecoder });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.VaeEncoder, OnnxModelPath = vaeEncoder });
                }
                else
                {
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Unet, OnnxModelPath = unetPath });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Tokenizer, OnnxModelPath = tokenizerPath });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Tokenizer2, OnnxModelPath = tokenizer2Path });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.TextEncoder, OnnxModelPath = textEncoderPath });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.TextEncoder2, OnnxModelPath = textEncoder2Path });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.VaeDecoder, OnnxModelPath = vaeDecoder });
                    modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.VaeEncoder, OnnxModelPath = vaeEncoder });
                }
            }
            else
            {
                modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Unet, OnnxModelPath = unetPath });
                modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.Tokenizer, OnnxModelPath = tokenizerPath });
                modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.TextEncoder, OnnxModelPath = textEncoderPath });
                modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.VaeDecoder, OnnxModelPath = vaeDecoder });
                modelSet.ModelConfigurations.Add(new OnnxModelConfig { Type = OnnxModelType.VaeEncoder, OnnxModelPath = vaeEncoder });
            }

            return modelSet;
        }

        public UpscaleModelSet CreateUpscaleModelSet(string name, string filename, UpscaleModelTemplate modelTemplate)
        {
            return new UpscaleModelSet
            {
                Name = name,
                Channels = 3,
                SampleSize = modelTemplate.SampleSize,
                ScaleFactor = modelTemplate.ScaleFactor,
                ModelConfigurations = new List<OnnxModelConfig> { new OnnxModelConfig { Type = OnnxModelType.Upscaler, OnnxModelPath = filename } },

                IsEnabled = true,
                DeviceId = _settings.DefaultDeviceId,
                ExecutionMode = _settings.DefaultExecutionMode,
                ExecutionProvider = _settings.DefaultExecutionProvider,
                InterOpNumThreads = _settings.DefaultInterOpNumThreads,
                IntraOpNumThreads = _settings.DefaultIntraOpNumThreads
            };
        }


        public ControlNetModelSet CreateControlNetModelSet(string name, ControlNetType controlNetType, DiffuserPipelineType pipelineType, string modelFilename, string annotationFilename)
        {
            var models = new List<OnnxModelConfig> { new OnnxModelConfig { Type = OnnxModelType.ControlNet, OnnxModelPath = modelFilename } };
            if (!string.IsNullOrEmpty(annotationFilename))
                models.Add(new OnnxModelConfig { Type = OnnxModelType.Annotation, OnnxModelPath = annotationFilename });

            return new ControlNetModelSet
            {
                Name = name,
                Type = controlNetType,
                PipelineType = pipelineType,
                ModelConfigurations = models,

                IsEnabled = true,
                DeviceId = _settings.DefaultDeviceId,
                ExecutionMode = _settings.DefaultExecutionMode,
                ExecutionProvider = _settings.DefaultExecutionProvider,
                InterOpNumThreads = _settings.DefaultInterOpNumThreads,
                IntraOpNumThreads = _settings.DefaultIntraOpNumThreads
            };
        }
    }
}
