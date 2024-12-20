# DiscordWikiBot
**DiscordWikiBot** [ˈdɪskɔːdˈwiːkibɒt] is a Discord bot that transforms [[wiki]] and {{template}} links in chat messages into actual links using MediaWiki APIs, and informs about recent changes in Wikimedia projects and on Translatewiki.net. It supports editing and deleting its own messages. It was originally developed for the Discord server of Russian Wikipedians. A private instance of the bot is available for Discord servers of Wikimedia communities.

DiscordWikiBot is cross-platform console app built with .NET Core. It uses [DSharpPlus](https://github.com/NaamloosDT/DSharpPlus), [WikiClientLibrary](https://github.com/CXuesong/WikiClientLibrary), and [EvtSource](https://github.com/3ventic/EvtSource) for most of heavy lifting. Its code is published under MIT licence.

## Installation
Keep in mind that the main wiki defined in `config.json` is required to be at least on [current LTS version of MediaWiki](https://www.mediawiki.org/wiki/Version_lifecycle). Older versions are not supported.

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
	// REQUIRED: User agent string.
	// Change the username if you modified DiscordWikiBot internals (not including configs).
	"userAgent": "DiscordWikiBot/{version} (https://w.wiki/4nm) (user:stjn)",

	// Link to the bot’s source code
	"repo":  "<https://github.com/stjohann/DiscordWikiBot> (C# / MIT)",

	// Use Discord’s buggy standard embeds for wikilinks
	"useDiscordLinkEmbeds": false,

	// Default domain for recent changes streams (only Wikimedia projects work here)
	"domain": "ru.wikipedia.org",
	
	// REQUIRED: Default language of the bot
	"lang": "ru",

	// REQUIRED: Default wikilink configuration
	"wiki": "https://ru.wikipedia.org/wiki/$1"
}
```

Non-system variables in `config.json` can be overridden per server (and some per channel) by members with ‘Manage server’ permission.

## Usage
When the bot is enabled, it will transform [[link syntax]] to real URLs to the pages of your wiki or its interwiki links, and will transform {{template syntax}} to real URLs to the templates of your wiki. To stop the bot from reacting to links in your message, wrap it into \` (\`[[example]]\`) or escape \[\[ symbols (with \\ before them).

DiscordWikiBot can be configured per server by server members with ‘Manage server’ permission. Available configuration includes the language of the bot, the default wiki URL, recent changes streams parameters etc. Up-to-date instructions for configuration of the bot [are provided on Meta-Wiki](https://meta.wikimedia.org/wiki/Discord#WikiBot).

## Versioning
DiscordWikiBot uses [semver](https://semver.org/) for versioning:

- Major versions (**X.0.0**) are changes that remove backwards-compatibility of any of the configuration files, including introducing new expectations from bot owners.
- Minor versions (**0.X.0**) are changes that introduce new features to the bot.
- Patch versions (**0.0.X**) are changes that fix existing code without introducing new features.
- Third-party bot maintainers can update the bot to minor/patch versions without any required changes.

Pull requests should, if possible, include a change in `DiscordWikiBot/DiscordWikiBot.csproj` file with an appropriate version change.

## Translations
Translations are done by volunteers [on translatewiki.net](https://translatewiki.net/wiki/Translating:DiscordWikiBot). Pull requests with simple translation changes will not be accepted (except for `en.json`).
