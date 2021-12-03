# DiscordWikiBot
**DiscordWikiBot** [ˈdɪskɔːdˈwiːkibɒt] is a Discord bot that transforms [[wiki]] and {{template}} links in chat messages into actual links using MediaWiki APIs, and informs about recent changes in Wikimedia projects and on Translatewiki.net. It supports editing and deleting its own messages. It was originally developed for the Discord server of Russian Wikipedians. A private instance of the bot is available for Discord servers of Wikimedia communities.

DiscordWikiBot is cross-platform console app built with .NET Core. It uses [DSharpPlus](https://github.com/NaamloosDT/DSharpPlus), [WikiClientLibrary](https://github.com/CXuesong/WikiClientLibrary), and [EvtSource](https://github.com/3ventic/EvtSource) for most of heavy lifting. Its code is published under MIT licence.

## Installation
1. Download the source files.
2. Create `token.txt` in project folder with a private token for your Discord bot. If you haven’t created your own Discord bot, [create it first](https://discordapp.com/developers/applications/me). Do not share your private token.
3. Change `config.json` to your needs according to instructions there.
4. Add `eventStreams.json` file containing only `{}` to the folder with `config.json` if you intend to use recent changes streams (Wikimedia projects only).
5. Compile the bot’s binaries using any compiler that supports .NET Core (use IDEs like Visual Studio or MonoDevelop or dotnet CLI).

**Important:** When developing or updating the bot, take care of the folder where the application is compiled. That’s where `eventStreams.json` and `overrides.json` are being stored when running it, and if you clean the folder accidentally, all the data will get lost.

## Configuration
The version in this repository is configured for Russian Wikipedia by default. Your instance of the bot can change this by changing values in `config.json`. Below is a short documentation for every available variable (remove lines starting with `//` if you’re going to copy from here). Required parameters are marked.

```js
{
	// Link to the bot’s source code
	"repo":  "<https://github.com/stjohann/DiscordWikiBot> (C# / MIT)",

	// Default domain for recent changes streams (only Wikimedia projects work here)
	"domain": "ru.wikipedia.org",
	
	// REQUIRED: Default language of the bot
	"lang": "ru",

	// REQUIRED: Default wiki link configuration
	"wiki": "https://ru.wikipedia.org/wiki/$1"
}
```

Most variables in `config.json` can be overridden per server by members with ‘Manage server’ permission.

## Usage
When the bot is enabled, it will transform [[link syntax]] to real URLs to the pages of your wiki or its interwiki links, and will transform {{template syntax}} to real URLs to the templates of your wiki. To stop the bot from reacting to links in your message, wrap it into \` (\`[[example]]\`) or escape [[ symbols (with \\ before them).

DiscordWikiBot can be configured per server by server members with ‘Manage server’ permission. Available configuration includes the language of the bot, the default wiki URL, recent changes streams parameters etc. Up-to-date instructions for configuration of the bot [are provided on Meta-Wiki](https://meta.wikimedia.org/wiki/Discord#WikiBot).
