using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DiscordWikiBot.Schemas
{
	/// <summary>
	/// Dictionary extension class for working with config data.
	/// </summary>
	/// <typeparam name="TKey">Config property key.</typeparam>
	/// <typeparam name="TValue">Config property value.</typeparam>
	public class ConfigData : Dictionary<string, object>
	{
		[JsonPropertyName("prefix")]
		public string Prefix { get; set; }

		[JsonPropertyName("userAgent")]
		public string UserAgent { get; set; }
		
		[JsonPropertyName("repo")]
		public string Repo { get; set; }
		
		[JsonPropertyName("answerBots")]
		public bool AnswerBots { get; set; }
		
		[JsonPropertyName("domain")]
		public string Domain { get; set; }

		[JsonPropertyName("lang")]
		public string Lang { get; set; }
		
		[JsonPropertyName("wiki")]
		public string Wiki { get; set; }
		
		[JsonPropertyName("translatewiki-channel")]
		public string TranslatewikiChannel { get; set; }
		
		[JsonPropertyName("translatewiki-lang")]
		public string TranslatewikiLang { get; set; }
		
		[JsonPropertyName("translatewiki-key")]
		public string TranslatewikiLastKey { get; set; }
	}
}
