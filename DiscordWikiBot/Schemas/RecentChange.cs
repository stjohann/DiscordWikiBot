using System;
using Newtonsoft.Json;

namespace DiscordWikiBot.Schemas
{
	/// <summary>
	/// MediaWiki RecentChange event.
	/// <para>Matches schema here: https://github.com/wikimedia/mediawiki-event-schemas/blob/master/jsonschema/mediawiki/recentchange/1.0.0.yaml </para>
	/// </summary>
	public partial class RecentChange
	{
		/// <summary>
		/// The URI identifying the jsonschema for this event.
		/// This may be just a short uri containing only the name and revision at the end of the URI path.
		/// </summary>
		[JsonProperty("$schema")]
		[JsonRequired]
		public string Schema { get; set; }

		/// <summary>
		/// Whether the change was marked as a bot edit.
		/// </summary>
		[JsonProperty("bot")]
		public bool Bot { get; set; }

		/// <summary>
		/// Recent change comment.
		/// </summary>
		[JsonProperty("comment")]
		public string Comment { get; set; }

		/// <summary>
		/// ID of the recentchange event.
		/// </summary>
		[JsonProperty("id")]
		public long Id { get; set; }

		/// <summary>
		/// Length of old and new change.
		/// </summary>
		[JsonProperty("length")]
		public OldNewProps Length { get; set; }

		/// <summary>
		/// Log action ID.
		/// </summary>
		[JsonProperty("log_id")]
		public long LogId { get; set; }

		/// <summary>
		/// Log action type.
		/// </summary>
		[JsonProperty("log_type")]
		public string LogType { get; set; }

		/// <summary>
		/// Log action name.
		/// </summary>
		[JsonProperty("log_action")]
		public string LogAction { get; set; }

		/// <summary>
		/// Log action comment.
		/// </summary>
		[JsonProperty("log_action_comment")]
		public string LogActionComment { get; set; }

		/// <summary>
		/// Any additional log parameters.
		/// </summary>
		[JsonProperty("log_params")]
		public dynamic LogParams { get; set; }

		/// <summary>
		/// Meta data object. All events schemas should have this.
		/// </summary>
		[JsonProperty("meta")]
		[JsonRequired]
		public MetaProps Metadata { get; set; }

		/// <summary>
		/// Whether the change was marked as a minor edit.
		/// </summary>
		[JsonProperty("minor")]
		public bool Minor { get; set; }

		/// <summary>
		/// ID of relevant namespace of affected page.
		/// This is -1 ("Special") for log events.
		/// </summary>
		[JsonProperty("namespace")]
		public int Namespace { get; set; }

		/// <summary>
		/// Comment parsed into simple HTML.
		/// </summary>
		[JsonProperty("parsedcomment")]
		public string ParsedComment { get; set; }

		/// <summary>
		/// Whether the edit was marked as patrolled.
		/// This property only exists if patrolling is supported for this event.
		/// </summary>
		[JsonProperty("patrolled")]
		public bool Patrolled { get; set; }

		/// <summary>
		/// Old and new revision IDs.
		/// </summary>
		[JsonProperty("revision")]
		public OldNewProps Revision { get; set; }

		/// <summary>
		/// Server name as specified in $wgServerName.
		/// </summary>
		[JsonProperty("server_name")]
		public string ServerName { get; set; }

		/// <summary>
		/// Server script path as specified in $wgScriptPath.
		/// </summary>
		[JsonProperty("server_script_path")]
		public string ServerScriptPath { get; set; }

		/// <summary>
		/// Server URL as specified in $wgCanonicalServer.
		/// </summary>
		[JsonProperty("server_url")]
		public Uri ServerUrl { get; set; }

		/// <summary>
		/// Unix timestamp.
		/// </summary>
		[JsonProperty("timestamp")]
		public long Timestamp { get; set; }

		/// <summary>
		/// Full page name, from Title::getPrefixedText.
		/// </summary>
		[JsonProperty("title")]
		public string Title { get; set; }

		/// <summary>
		/// Type of recentchange event (rc_type).
		/// One of "edit", "new", "log", "categorize", or "external".
		/// <para>See: https://www.mediawiki.org/wiki/Manual:Recentchanges_table#rc_type </para>
		/// </summary>
		[JsonProperty("type")]
		public string Type { get; set; }

		/// <summary>
		/// Username of author of the change.
		/// </summary>
		[JsonProperty("user")]
		public string User { get; set; }

		/// <summary>
		/// Wiki ID as specified in $wgDBname.
		/// </summary>
		[JsonProperty("wiki")]
		public string Wiki { get; set; }
	}

	public partial class RecentChange
	{
		public static RecentChange FromJson(string json) => JsonConvert.DeserializeObject<RecentChange>(json);

		/// <summary>
		/// Meta data object. All events schemas should have this.
		/// </summary>
		public partial class MetaProps
		{
			/// <summary>
			/// The domain the event pertains to.
			/// </summary>
			[JsonProperty("domain")]
			[JsonRequired]
			public string Domain { get; set; }

			/// <summary>
			/// The time stamp of the event, in ISO8601 format.
			/// </summary>
			[JsonProperty("dt")]
			[JsonRequired]
			public DateTime DateTime { get; set; }

			/// <summary>
			/// The unique ID of this event.
			/// </summary>
			[JsonProperty("id")]
			[JsonRequired]
			public string Id { get; set; }

			/// <summary>
			/// The unique ID of the request that caused the event.
			/// </summary>
			[JsonProperty("request_id")]
			public string RequestId { get; set; }

			/// <summary>
			/// The queue topic name this message belongs to.
			/// </summary>
			[JsonProperty("topic")]
			[JsonRequired]
			public string Topic { get; set; }

			/// <summary>
			/// The unique URI identifying the event.
			/// </summary>
			[JsonProperty("uri")]
			[JsonRequired]
			public Uri Uri { get; set; }

			[JsonProperty("partition")]
			public long Partition { get; set; }

			[JsonProperty("offset")]
			public long Offset { get; set; }
		}

		/// <summary>
		/// Old and new values of a property.
		/// </summary>
		public partial class OldNewProps
		{
			/// <summary>
			/// Value of a property after doing this change.
			/// </summary>
			[JsonProperty("new")]
			public long New { get; set; } = 0;

			/// <summary>
			/// Value of a property before doing this change.
			/// </summary>
			[JsonProperty("old")]
			public long Old { get; set; } = 0;
		}
	}
}
