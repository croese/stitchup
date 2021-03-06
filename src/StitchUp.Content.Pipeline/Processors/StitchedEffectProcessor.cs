using System;
using System.IO;
using System.Text;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using StitchUp.Content.Pipeline.FragmentLinking.CodeModel;
using StitchUp.Content.Pipeline.FragmentLinking.Compiler;
using StitchUp.Content.Pipeline.FragmentLinking.EffectModel;
using StitchUp.Content.Pipeline.FragmentLinking.Generator;
using StitchUp.Content.Pipeline.Graphics;

namespace StitchUp.Content.Pipeline.Processors
{
	[ContentProcessor(DisplayName = "Stitched Effect - StitchUp")]
	public class StitchedEffectProcessor : ContentProcessor<StitchedEffectContent, CompiledEffectContent>
	{
		public override CompiledEffectContent Process(StitchedEffectContent input, ContentProcessorContext context)
		{
			StitchedEffectSymbol stitchedEffect = StitchedEffectBuilder.BuildStitchedEffect(input, context);

			// Find out which shader profile to attempt to compile for first.
            ShaderProfile minimumShaderProfile = GetMinimumTargetShaderProfile(stitchedEffect);

            foreach (ShaderProfile shaderProfile in Enum.GetValues(typeof(ShaderProfile)))
            {
                if (shaderProfile < minimumShaderProfile)
                    continue;

                CompiledEffectContent compiledEffect;
                if (AttemptEffectCompilation(context, input, stitchedEffect, shaderProfile, out compiledEffect))
                    return compiledEffect;
            }

		    throw new InvalidContentException(
                "Could not find a shader profile compatible with this stitched effect.",
                input.Identity);
		}

        private static bool AttemptEffectCompilation(
            ContentProcessorContext context, StitchedEffectContent input,
            StitchedEffectSymbol stitchedEffect, ShaderProfile shaderProfile,
            out CompiledEffectContent compiledEffect)
        {
            // Generate effect code.
        	StringWriter writer = new StringWriter();
			EffectCodeGenerator codeGenerator = new EffectCodeGenerator(stitchedEffect, shaderProfile, writer);
            codeGenerator.GenerateCode();
        	string effectCode = writer.ToString();

            // Save effect code so that if there are errors, we'll be able to view the generated .fx file.
            string tempEffectFile = GetTempEffectFileName(input);
            File.WriteAllText(tempEffectFile, effectCode, Encoding.GetEncoding(1252));

            // Process effect code.
            EffectProcessor effectProcessor = new EffectProcessor
            {
                DebugMode = EffectProcessorDebugMode.Auto,
                Defines = null
            };

            EffectContent effectContent = new EffectContent
            {
                EffectCode = effectCode,
                Identity = new ContentIdentity(tempEffectFile),
                Name = input.Name
            };

            try
            {
                compiledEffect = effectProcessor.Process(effectContent, context);

				// This is only needed if the compilation was successful - if it failed,
				// a more specific error message (with link to the generated file)
				// will be shown to the user.
				context.Logger.LogImportantMessage(string.Format("{0} : Stitched effect generated (double-click this message to view).", tempEffectFile));

                return true;
            }
            catch (InvalidContentException ex)
            {
                if (ErrorIndicatesNeedForShaderModel3(ex.Message))
                {
                    compiledEffect = null;
                    return false;
                }
                throw;
            }
        }

		private static bool ErrorIndicatesNeedForShaderModel3(string message)
		{
			return message.Contains("error X5608")
				|| message.Contains("error X5609")
				|| message.Contains("error X4502");
		}

		// Use a semi-unique filename so that multiple stitched effects can be worked on and the resulting
		// effect files opened simultaneously. This does mean you'll end up with several of the resulting
		// effect files in your temp folder, but they're quite small files.
		private static string GetTempEffectFileName(StitchedEffectContent input)
		{
			return Path.Combine(Path.GetTempPath(), Path.ChangeExtension(Path.GetFileName(input.Identity.SourceFilename), ".fx"));
		}

		private static ShaderProfile GetMinimumTargetShaderProfile(StitchedEffectSymbol stitchedEffect)
		{
			foreach (ShaderProfile shaderProfile in Enum.GetValues(typeof(ShaderProfile)))
				if (stitchedEffect.CanBeCompiledForShaderProfile(shaderProfile))
					return shaderProfile;
			throw new InvalidContentException("Could not find shader profile compatible with this stitched effect.");
		}
	}
}