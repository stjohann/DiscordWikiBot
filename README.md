# DiscordWikiBot
**DiscordWikiBot** [ˈdɪskɔːdˈwiːkibɒt] is a bot for Discord chat of Russian Wikipedians that can transform your [[wiki]] and {{template}} links into actual links using APIs, and send recent changes into the channels on server moderators’ discretion. It is built using [DSharpPlus](https://github.com/NaamloosDT/DSharpPlus) and [XmlRcs](https://github.com/huggle/XMLRCS/tree/master/clients/c%23/XmlRcs) libraries.

DiscordWikiBot is published under MIT licence.

## Usage
1. Download the source files.
2. Create `token.txt` in project folder with a private token for your Discord bot. If you haven’t created your own Discord bot, [create it first](https://discordapp.com/developers/applications/me). Do not share your private token.
3. Change `config.json` to your needs according to instructions there.
4. Add `eventStreams.json` file (see below) to the folder with a project if you intend to use recent changes streams.

## Configuration
The default config file should look more or less like this (without comments, they are provided here for guidance):
```js
{
	// Domain for EventStreams function, set to "" if it is not needed for the server
	"domain": "ru.wikipedia.org",
	
	// Language of the bot
	"lang": "ru",

	//
	// Notifications about new messages on Translatewiki
	// Remove both fields from the config if you won’t be using it at all
	//
	// Channel for notifications: should be the one bot has access to, otherwise it will fail
	"translatewiki-channel": "448985285603098624",
	// Language from which notifications should be sent
	"translatewiki-language": "ru",

	// Default wiki link configuration
	"wiki": "https://ru.wikipedia.org/wiki/$1"
}
```

You can configure the bot per guild using special commands (use command `!help` if you are stuck):
+ `!guildLang en` would change the language of bot to English. Works only for ISO 639-2 languages.
+ `!guildWiki <https://en.wikipedia.org/wiki/$1>` would change default link of bot to English Wikipedia.
+ `!guildDomain en.wikipedia.org` would change default EventStreams target domain to en.wikipedia.org (works only for Wikimedia projects).
All commands above are available only in `#moderators` channel (which, ideally, you should make the most private one on the server) and are reversible back to default using `-` as a value (`!guildLang -` etc.).

In the future it is planned to have sensitive commands check for user’s permissions, but for now it doesn’t do this. Please don’t provide access to #moderators channel to non-trusted users when using this bot.

## EventStreams
DiscordWikiBot uses [EventStreams](https://wikitech.wikimedia.org/wiki/EventStreams) through proxy of [XmlRcs](https://wikitech.wikimedia.org/wiki/XmlRcs), that were developed for [Huggle](https://en.wikipedia.org/wiki/Wikipedia:Huggle), an anti-vandalism tool. This feature is not suitable for usage outside of Wikimedia projects.

To use EventStreams with your favourite Wikimedia project, you would have to do two things:

1. Set `domain` setting in `config.json` with domain of your project (such as `ru.wikipedia.org` or `www.wikidata.org`).
2. Create `eventStreams.json` file in the same folder as your `config.json`.

You can do the second step in different ways: either you can create the file with the content `{}` (eventually having an empty JSON object) and use `#moderators` channel for later configuration (commands are `!openStream` and `!closeStream`, use command `!help` if you are stuck), or you can configure it manually:

1. Copy IDs of your preferred channel (turn on Developer Mode in your Discord settings and right click on any channel).
2. Set it up like an example below (of course, change `000000000000000000` to correct channel IDs), where the tool would send the messages about all the changes in MediaWiki namespace and about the changes at `Википедия:Запросы к администраторам/Быстрые` page that are larger than 900 bytes:

```js
{
	// Namespace example (brackets are required), use {{NAMESPACENUMBER}} or other means to get namespace number
	"<8>": [
		"000000000000000000"
	],
	// Page example, not having brackets would match page title
	// The integer after a pipe is minimum length of the revision
	"Википедия:Запросы к администраторам/Быстрые": [
		"000000000000000000|900"
	]
}
```

Please note: EventStreams are somewhat unreliable and developers bear no responsibility if it does not work as reliable as you could’ve expected it because it uses an external library.
