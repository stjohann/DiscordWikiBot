//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU Lesser General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU Lesser General Public License for more details.

// Copyright (c) Petr Bena benapetr@gmail.com

using System;
using System.Collections.Generic;
using System.Text;

namespace XmlRcs
{
    /// <summary>
    /// Recent change item as provided by XmlRcs, if any string was not provided, it will be null
    /// You can also run EmptyNulls() in order to initialize all null strings with empty string
    /// </summary>
    public class RecentChange
    {
        /// <summary>
        /// Type of a change
        /// </summary>
        public enum ChangeType
        {
            /// <summary>
            /// Regular edit
            /// </summary>
            Edit,
            /// <summary>
            /// Log
            /// </summary>
            Log,
            /// <summary>
            /// New page
            /// </summary>
            New,
            /// <summary>
            /// Unknown type of change
            /// </summary>
            Unknown
        }

        /// <summary>
        /// Set all strings that contained unknown value to empty string, this can be useful in case you don't care if value
        /// was known or not
        /// </summary>
        public void EmptyNulls()
        {
            if (this.Wiki == null)
                this.Wiki = "";
            if (this.ServerName == null)
                this.ServerName = "";
            if (this.Title == null)
                this.Title = "";
            if (this.User == null)
                this.User = "";
            if (this.Summary == null)
                this.Summary = "";
        }
        /// <summary>
        /// Internal name of wiki
        /// </summary>
        public string Wiki = null;
        /// <summary>
        /// Name of server
        /// </summary>
        public string ServerName = null;
        /// <summary>
        /// Name of page
        /// </summary>
        public string Title = null;
        /// <summary>
        /// Namespace
        /// </summary>
        public int Namespace = 0;
        /// <summary>
        /// Revision id
        /// </summary>
        public int RevID = 0;
        /// <summary>
        /// Old id, this is ID
        /// </summary>
        public int OldID = 0;
        /// <summary>
        /// Name of user who did this change
        /// </summary>
        public string User = null;
        /// <summary>
        /// Whether the change was flagged as bot edit
        /// </summary>
        public bool Bot = false;
        /// <summary>
        /// Patrolled
        /// </summary>
        public bool Patrolled = false;
        /// <summary>
        /// Minor
        /// </summary>
        public bool Minor = false;
        public ChangeType Type = ChangeType.Unknown;
        /// <summary>
        /// New length of a change
        /// </summary>
        public int LengthNew = 0;
        /// <summary>
        /// Old length of a change
        /// </summary>
        public int LengthOld = 0;
        /// <summary>
        /// Summary of change
        /// </summary>
        public string Summary = null;
        /// <summary>
        /// RAW XML text
        /// </summary>
        public string OriginalXml;
        /// <summary>
        /// If no timestamp was provided this will equal minimal time
        /// </summary>
        public DateTime Timestamp = DateTime.MinValue;
    }
}
