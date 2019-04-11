using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Entities;
using DSharpPlus.Entities;

namespace DiscordWikiBot
{
	class LocalisedHelpFormatter : IHelpFormatter
	{
		private string Command;
		private DiscordEmbedBuilder EmbedBuilder { get; }

		public LocalisedHelpFormatter()
		{
			EmbedBuilder = new DiscordEmbedBuilder().WithColor(new DiscordColor(0x72777d));
		}

		public IHelpFormatter WithCommandName(string name)
		{
			Command = name;
			EmbedBuilder.WithTitle(Locale.GetMessage("help-title-command", "en", name, Config.GetValue("prefix")));

			return this;
		}

		public IHelpFormatter WithDescription(string description)
		{
			// Override help command
			if (this.Command == "help")
			{
				description = "help-command";
			}

			EmbedBuilder.WithDescription(Locale.GetMessage(description, "en"));
			return this;
		}

		public IHelpFormatter WithGroupExecutable()
		{
			EmbedBuilder.WithFooter(Locale.GetMessage("help-group-standalone", "en"));

			return this;
		}

		public IHelpFormatter WithAliases(IEnumerable<string> aliases)
		{
			EmbedBuilder.AddField(Locale.GetMessage("help-aliases", "en"), string.Join(", ", aliases));

			return this;
		}

		public IHelpFormatter WithArguments(IEnumerable<CommandArgument> arguments)
		{
			string args = string.Join("\n",
				arguments.Select(xarg =>
				{
					string desc = xarg.Description;
					string def = "";
					string optional = "";

					// Override help command
					if (Command == "help")
					{
						desc = "help-command-" + xarg.Name;
					}

					// Provide optional information
					if (xarg.Description.Length > 0)
					{
						desc = Locale.GetMessage("separator", "en", Locale.GetMessage(desc, "en"));
					}

					if (xarg.DefaultValue != null && xarg.DefaultValue.ToString() != "")
					{
						def = " " + Locale.GetMessage("help-default", "en", xarg.DefaultValue.ToString());

						if (xarg.IsOptional)
						{
							optional = ", " + Locale.GetMessage("help-optional", "en");
						}
					}

					return string.Format("`{0}` (_{1}{2}_){3}{4}",
						xarg.Name,
						xarg.Type.ToUserFriendlyName(),
						optional,
						desc,
						def
					);
				})
			);

			EmbedBuilder.AddField(Locale.GetMessage("help-arguments", "en"), args);
			return this;
		}

		public IHelpFormatter WithSubcommands(IEnumerable<Command> subcommands)
		{
			string header = Locale.GetMessage("help-subcommands", "en");
			if (Command == null)
			{
				header = Locale.GetMessage("help-commands", "en");
			}
			string list = string.Join("\n", subcommands.Select(xc => {
				if (!xc.IsHidden)
				{
					string comm = $"{xc.Name}";
					if (Command != null)
					{
						comm = $"{Command} {comm}";
					}
					string descId = (xc.Name == "help" ? "help-command" : xc.Description);

					string desc = Locale.GetMessage("separator", "en", Locale.GetMessage(descId, "en"));
					return Locale.GetMessage("bullet", "en", $"`{Config.GetValue("prefix")}{comm}{desc}`");
				}

				return null;
			}).Where(s => !string.IsNullOrEmpty(s)));

			EmbedBuilder.AddField(header, list);
			return this;
		}

		public CommandHelpMessage Build()
		{
			if (Command == null)
			{
				// Add a link to the source code repository
				string description = Locale.GetMessage("help-all", "en");
				if (Config.GetValue("repo") != null)
				{
					description = Locale.GetMessage("help-repo", "en", Config.GetValue("repo")) + description;
				}

				EmbedBuilder
					.WithTitle(Locale.GetMessage("help-title", "en"))
					.WithDescription(description);
			}
			return new CommandHelpMessage(embed: EmbedBuilder.Build());
		}
	}
}
