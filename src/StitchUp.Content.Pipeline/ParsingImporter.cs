using System;
using System.IO;
using Microsoft.Xna.Framework.Content.Pipeline;
using StitchUp.Content.Pipeline.FragmentLinking.CodeModel;
using StitchUp.Content.Pipeline.FragmentLinking.Parser;
using ErrorEventArgs = StitchUp.Content.Pipeline.FragmentLinking.Parser.ErrorEventArgs;

namespace StitchUp.Content.Pipeline
{
	public abstract class ParsingImporter<T, TParser> : ContentImporter<T>
		where T : ContentItem
		where TParser : Parser
	{
		protected abstract string ImporterName { get; }

		public override T Import(string filename, ContentImporterContext context)
		{
			if (context == null)
				throw new ArgumentNullException("context");
			if (string.IsNullOrEmpty(filename))
				throw new ArgumentException("Filename cannot be null or empty.", "filename");
			FileInfo info = new FileInfo(filename);
			if (!info.Exists)
				throw new FileNotFoundException("File not found", filename);

			ContentIdentity identity = new ContentIdentity(info.FullName, ImporterName);

			string text = File.ReadAllText(filename);
			T content = BeginParse(filename, text, identity);
			if (content == null)
			{
				Lexer lexer = new Lexer(filename, text);
				lexer.Error += (sender, e) => ThrowParserException(e, info);
				Token[] tokens = lexer.GetTokens();

				TParser parser = GetParser(filename, tokens, identity);
				parser.Error += (sender, e) => ThrowParserException(e, info);

				content = CreateContent(parser, identity);
			}
			content.Identity = identity;
			return content;
		}

		protected virtual T BeginParse(string fileName, string text, ContentIdentity identity)
		{
			return null;
		}

		protected abstract TParser GetParser(string fileName, Token[] tokens, ContentIdentity identity);
		protected abstract T CreateContent(TParser parser, ContentIdentity identity);

		private static void ThrowParserException(ErrorEventArgs e, FileInfo info)
		{
			string identifier = string.Format("{0},{1}", e.Position.Line + 1, e.Position.Column + 1);
			ContentIdentity contentIdentity = new ContentIdentity(info.FullName, "Fragment Importer", identifier);
			throw new InvalidContentException(e.Message, contentIdentity);
		}
	}
}