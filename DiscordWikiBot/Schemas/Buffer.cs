using System.Collections.Generic;

namespace DiscordWikiBot.Schemas
{
	/// <summary>
	/// Dictionary extension class to store a specified number of items.
	/// </summary>
	public class Buffer<TKey, TValue> : Dictionary<TKey, TValue>
	{
		/// <summary>
		/// Maximum number of items.
		/// </summary>
		public int MaxItems { get; set; }

		/// <summary>
		/// Collection of ordered keys.
		/// </summary>
		private Queue<TKey> orderedKeys = new Queue<TKey>();

		/// <summary>
		/// Modified method for adding new items.
		/// </summary>
		/// <param name="key">Key.</param>
		/// <param name="value">Value.</param>
		public new void Add(TKey key, TValue value)
		{
			orderedKeys.Enqueue(key);
			if (this.MaxItems != 0 && this.Count >= MaxItems)
			{
				this.Remove(orderedKeys.Dequeue());
			}

			base.Add(key, value);
		}
	}
}
