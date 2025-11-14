using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WikiClientLibrary.Client;

namespace DiscordWikiBot.Tests
{
#if !DEBUG
	[Ignore]
#endif
	[TestClass]
	public class LinkingTests
	{
		[TestMethod]
		public void BasicLink()
		{
			Assert.AreEqual(
				@"Ссылка: [[[`Test link`]]]( <https://ru.wikipedia.org/wiki/Test_link> )",
				TestMessage("[[test link]]")
			);
		}

		[TestMethod]
		public void BasicEscapedLink()
		{
			Assert.AreEqual(
				@"Ссылка: [[[`Test (disambiguation)`]]]( <https://ru.wikipedia.org/wiki/Test_%28disambiguation%29> )",
				TestMessage("[[test_(disambiguation)]]")
			);
		}

		[TestMethod]
		public void MultipleLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`Кот`]]]( <https://ru.wikipedia.org/wiki/Кот> ), [[[`Пёс`]]]( <https://ru.wikipedia.org/wiki/Пёс> )",
				TestMessage("[[Кот]]о[[пёс]]")
			);
		}

		[TestMethod]
		public void DuplicateLinks()
		{
			Assert.AreEqual(@"Ссылка: [[[`Заглавная страница`]]]( <https://ru.wikipedia.org/wiki/Заглавная_страница> )", TestMessage(@"
				[[Заглавная страница]]
				{{:Заглавная страница}}
				https://ru.m.wikipedia.org/wiki/Заглавная_страница
			"));
		}

		[TestMethod]
		public void GenderedUserLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`Участник:Stjn`]]]( <https://ru.wikipedia.org/wiki/Участник:Stjn> ), [[[`Участница:Udacha`]]]( <https://ru.wikipedia.org/wiki/Участница:Udacha> ), [[[`Участник:Js`]]]( <https://ru.wikipedia.org/wiki/Участник:Js> )",
				TestMessage(@"
					[[user:stjn]]
					[[user:Udacha]]
					[[user:js]]
				")
			);
		}

		[TestMethod]
		public void TemplateLinks()
		{
			// Basic tests with translations
			Assert.AreEqual(
				@"Ссылки: [{{`С отвратительным дизайном`}}]( <https://ru.wikipedia.org/wiki/Шаблон:С_отвратительным_дизайном> ), [{{`Тест`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Тест> ), [{{`Тест 2`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Тест_2> ), [{{`MediaWiki:Lang`}}]( <https://ru.wikipedia.org/wiki/MediaWiki:Lang> )",
				TestMessage(@"
					{{С отвратительным дизайном}}
					{{subst:тест}}
					{{подст:тест 2}}
					{{int:lang}}
					{{подст:int:lang}}
				")
			);

			// Namespace tests and complications
			Assert.AreEqual(
				@"Ссылки: [{{`MediaWiki:Lang`}}]( <https://ru.wikipedia.org/wiki/MediaWiki:Lang> ), [{{`Модуль:Math`}}]( <https://ru.wikipedia.org/wiki/Модуль:Math> ), [{{`:Тест`}}]( <https://ru.wikipedia.org/wiki/Тест> )",
				TestMessage(@"
					{{subst:внутр:lang}}
					{{#invoke:Math}}
					{{#вызвать:Math}}
					[[Module:Math]]
					{{:тест}}
				")
			);
		}

		[TestMethod]
		public void MagicWordLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`Шаблон:PAGENAME`]]]( <https://ru.wikipedia.org/wiki/Шаблон:PAGENAME> ), [{{`SERVERNAME: test`}}]( <https://ru.wikipedia.org/wiki/Шаблон:SERVERNAME:_test> ), [{{`Служебная:RecentChanges`}}]( <https://ru.wikipedia.org/wiki/Служебная:RecentChanges> ), [{{`Tag test`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Tag_test> )",
				TestMessage(@"
					// Should return a template name, but currently cannot
					{{pagename}}
					{{SERVERNAME}}
					{{safesubst:SERVERNAME}}
					[[Template:PAGENAME]]
					{{servername}}
					{{SERVERNAME: test}}
					{{PAGENAME|test}}
					{{Special:RecentChanges}}
					{{#special:RecentChanges}}
					{{#tag:ref}}
					{{tag test}}
					{{subst:#time:d.m.Y H:i}}
					{{formatnum:223}}
					{{GENDER:stjn}}
				")
			);
		}

		[TestMethod]
		public void MobileLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`Тест`]]]( <https://ru.wikipedia.org/wiki/Тест> ), [[[`Заглавная страница`]]]( <https://ru.wikipedia.org/wiki/Заглавная_страница> ), [[[`Википедия:Форум/Общий`]]]( <https://ru.wikipedia.org/wiki/Википедия:Форум/Общий> )",
				TestMessage(@"
					[[Тест]]
					https://ru.m.wikipedia.org/wiki/Заглавная_страница
					https://en.m.wikipedia.org/wiki/Main_Page
					https://ru.m.wikipedia.org/wiki/Википедия:Форум?action=history
					[общий форум]( <https://ru.m.wikipedia.org/wiki/Википедия:Форум/Общий> )
				")
			);

			Assert.AreEqual(
				@"Ссылка: [[[`API:Etiquette`]]]( <https://www.mediawiki.org/wiki/API:Etiquette> )",
				TestMessage(@"https://m.mediawiki.org/wiki/API:Etiquette", "https://www.mediawiki.org/wiki/$1")
			);
		}

		[TestMethod]
		public void EmbeddableLink()
		{
			Assert.AreEqual(
				@"Ссылка: [[[`phab:T2001`]]]( https://phabricator.wikimedia.org/T2001 )",
				TestMessage(@"[[phab:T2001]]")
			);
		}

		[TestMethod]
		public void NamespaceAliasLink()
		{
			Assert.AreEqual(
				@"Ссылка: [[[`Википедия:Страшное место`]]]( <https://ru.wikipedia.org/wiki/Википедия:Страшное_место> )",
				TestMessage("[[ВП:Страшное место]]")
			);
		}

		[TestMethod]
		public void NonCapitalisedLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`wikt:пёс`]]]( <https://ru.wiktionary.org/wiki/пёс> ), [[[`wikt:MediaWiki:Common.js`]]]( <https://ru.wiktionary.org/wiki/MediaWiki:Common.js> ), [[[`ხაჭაპური`]]]( <https://ru.wikipedia.org/wiki/ხაჭაპური> )",
				TestMessage("[[wikt:пёс]] [[wikt:mediawiki:common.js]] [[ხაჭაპური]]")
			);
		}

		[TestMethod]
		public void BasicInterwikiLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`en:Wikipedia:Sandbox`]]]( <https://en.wikipedia.org/wiki/Wikipedia:Sandbox> ), [[[`fr:Wikipédia:Accueil principal`]]]( <https://fr.wikipedia.org/wiki/Wikipédia:Accueil_principal> ), [{{`De:test`}}]( <https://ru.wikipedia.org/wiki/Шаблон:De:test> )",
				TestMessage(@"
					[[en:wikipedia:sandbox]]
					[[fr:]]
					{{de:test}}
				")
			);
		}

		[TestMethod]
		public void NonWikiInterwikiLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`toollabs:guc`]]]( <https://iw.toolforge.org/guc> ), [[[`google:`]]]( <https://www.google.com/search?q=> ), [[[`google:lmgtfy`]]]( <https://www.google.com/search?q=lmgtfy> )",
				TestMessage(@"
					[[toollabs:guc]]
					[[google:]]
					[[google:lmgtfy]]
				")
			);
		}

		[TestMethod]
		public void NestedInterwikiLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`en:wikt:test`]]]( <https://en.wiktionary.org/wiki/test> ), [[[`en:wikt:MediaWiki:Common.js`]]]( <https://en.wiktionary.org/wiki/MediaWiki:Common.js> ), [[[`google:de:test`]]]( <https://www.google.com/search?q=de:test> )",
				TestMessage(@"
					[[en:wikt:test]]
					[[en:wikt:mediawiki:common.js]]
					[[google:de:test]]
				")
			);
		}

		[TestMethod]
		public void EmojiInterwikiLink()
		{
			Assert.AreEqual(
				@"Ссылка: [[[`meta:Discord`]]]( <https://meta.wikimedia.org/wiki/Discord> )",
				TestMessage("[[<:meta:873203055804436513>Discord]]")
			);
		}

		[TestMethod]
		public void WeirdlyAdjacentLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`Каждый`]]]( <https://ru.wikipedia.org/wiki/Каждый> ), [[[`Охотник`]]]( <https://ru.wikipedia.org/wiki/Охотник> ), [{{`Желает`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Желает> ), [{{`Знать`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Знать> ), [{{`Фазан`}}]( <https://ru.wikipedia.org/wiki/Шаблон:Фазан> )",
				TestMessage(@"
					[[[каждый]]
					[[охотник]]]
					{{желает}}}
					{{{знать}}
					{{{где}}}
					{{{{сидит}}}}
					{{{{{{фазан}}
				")
			);
		}

		[TestMethod]
		public void WeirdLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`Test#One Two`]]]( <https://ru.wikipedia.org/wiki/Test#One_Two> ), [[[`Tamzin`]]]( <https://ru.wikipedia.org/wiki/Tamzin> )",
				TestMessage("[[:Test#One%20Two]] [[Tam&#x200F;zin]]")
			);

			Assert.AreEqual(
				@"Ссылки: [[[`Файл:Rainbow lorikeet.jpg`]]]( <https://ru.wikipedia.org/wiki/Файл:Rainbow_lorikeet.jpg> ), [{{`)!`}}]( <https://ru.wikipedia.org/wiki/Шаблон:%29!> )",
				TestMessage("[[File:Rainbow lorikeet.jpg%20]] {{)!}}")
			);

			Assert.AreEqual(
				@"Ссылки: [[[`wikt:Gift`]]]( <https://ru.wiktionary.org/wiki/Gift> ), [[[`ja:おちんちん`]]]( <https://ja.wikipedia.org/wiki/おちんちん> ), [[[`Википедия:ЗЛВ`]]]( <https://ru.wikipedia.org/wiki/Википедия:ЗЛВ> ), [{{`MediaWiki:Mainpage`}}]( <https://ru.wikipedia.org/wiki/MediaWiki:Mainpage> )",
				TestMessage(@"
					[[wikt::Gift]]
					[[:ja : おちんちん]]
					[[: Википедия:ЗЛВ]]
					[[Википедия: ЗЛВ]]
					{{ int: mainpage }}
				")
			);

			Assert.AreEqual(
				@"Ссылки: [[[`\\`]]]( <https://ru.wikipedia.org/wiki/%5C%5C> ), [[[`Foo`]]]( <https://ru.wikipedia.org/wiki/Foo> ), [[[`Nested`]]]( <https://ru.wikipedia.org/wiki/Nested> ), [[[`Служебная:Search/insource:/Спецыялізуецца ў \./`]]]( <https://ru.wikipedia.org/wiki/Служебная:Search/insource:/Спецыялізуецца_ў_%5C./> )",
				TestMessage(@"
					[[\\]]
					[[foo|bar|baz]]
					[[nested|[pipes]]]
					[[Special:Search/insource:/Спецыялізуецца ў \./]]
				")
			);
		}

		[TestMethod]
		public void MarkdownQuirks()
		{

			Assert.AreEqual(
				@"Ссылки: [[[`` ` ``]]]( <https://ru.wikipedia.org/wiki/%60> ), [[[``en:` ``]]]( <https://en.wikipedia.org/wiki/%60> ), [[[`*`]]]( <https://ru.wikipedia.org/wiki/*> ), [[[``Unsupported titles/f`num``num`k``]]]( <https://ru.wikipedia.org/wiki/Unsupported_titles/f%60num%60%60num%60k> )",
				TestMessage(@"
					[[`]]
					[[:en:\`]]
					[[*]]
					[[\*]]
					[[Unsupported titles/f`num``num`k]]
					[[Unsupported titles/f\`num\`\`num\`k]]
				")
			);
		}

		[TestMethod]
		public void SupportedInvalidLinks()
		{
			Assert.AreEqual(
				@"Ссылки: [[[`//`]]]( <https://ru.wikipedia.org/wiki///> ), [[[`//Khara Hais Local Municipality`]]]( <https://ru.wikipedia.org/wiki///Khara_Hais_Local_Municipality> )",
				TestMessage(@"
					[[//]]
					[[//Khara Hais Local Municipality]]
				")
			);

			Assert.AreEqual(
				@"Ссылка: [[[`google:test test test`]]]( <https://www.google.com/search?q=test+test+test> )",
				TestMessage(@"
					[[google:test test test]]
				")
			);
		}

		[TestMethod]
		public void InvalidLinks()
		{
			Assert.AreEqual("", TestMessage(@"
				[[ ]]
				{{ }}
				[[#top]]
				{{/test}}
				[[https://ru.wikipedia.org/wiki/Ithappens]]
				[[<test>]]
				[[ВП:]]
				[[Eh bien, mon prince. Gênes et Lucques ne sont plus que des apanages, des поместья, de la famille Buonaparte. Non, je vous préviens que si vous ne me dites pas que nous avons la guerre, si vous vous permettez encore de pallier toutes les infamies, toutes les atrocités de cet Antichrist (ma parole, j'y crois) — je ne vous connais plus, vous n'êtes plus mon ami, vous n'êtes plus мой верный раб, comme vous dites]]
			"));
		}

		[TestMethod]
		public void LinksInIgnoredBlocks()
		{
			// Code blocks
			Assert.AreEqual("Ссылка: [[[`Escaped`]]]( <https://ru.wikipedia.org/wiki/Escaped> )", TestMessage(@"
				`[[one-liner]]` ``[[double]]``
				`[[multiline 1]]
				[[multiline 2]]`

				\`\`\`
				\`[[escaped]]\`
				\`\`\`

				``[[double multiline 1]]
				[[double multiline 2]]``

				<nowiki>[[test]]</nowiki>
				<nowiki>
				[[nowiki]]
				[[nowiki 2]]
				</nowiki>

				```
				[[code block]]
				```
			"));

			// Quotes
			Assert.AreEqual("", TestMessage(@"
				> quote block example [[test]]

				>>> test
				[[ignore everything after triple quote block]]
			"));
		}

		[TestMethod]
		public void LinksInSpoilerBlocks()
		{
			Assert.AreEqual(
				@"Ссылка: ||[[[`Test`]]]( <https://ru.wikipedia.org/wiki/Test> )||",
				TestMessage("||[[Test]]||")
			);

			Assert.AreEqual(
				@"Ссылки: [[[`A`]]]( <https://ru.wikipedia.org/wiki/A> ), ||[[[`B`]]]( <https://ru.wikipedia.org/wiki/B> )||, ||[[[`C`]]]( <https://ru.wikipedia.org/wiki/C> )||, [[[`D`]]]( <https://ru.wikipedia.org/wiki/D> )",
				TestMessage("[[A]] ||[[B]] [[C]]|| [[D]]")
			);
		}

		/// <summary>
		/// Format a call to <see cref="Linking.PrepareMessage" />
		/// </summary>
		/// <param name="str">String to be tested.</param>
		/// <param name="format">Default link format.</param>
		/// <returns></returns>
		private static string TestMessage(string str, string format = "https://ru.wikipedia.org/wiki/$1")
		{
			// Remove starting spaces
			str = Regex.Replace(str, @"^[^\S\n\r]+", string.Empty, RegexOptions.Multiline);
			return Linking.PrepareMessage(str, "ru", format);
		}

		[ClassInitialize]
		public static void DiscordSetup(TestContext ctx)
		{
			Program.Client = new(new()
			{
				Token = "-",
			});
			Config.Init();
			Program.WikiClient = new WikiClient
			{
				ClientUserAgent = Program.GetBotUserAgent(),
			};
			Locale.Init();
			Linking.Init().Wait();
		}
	}
}
