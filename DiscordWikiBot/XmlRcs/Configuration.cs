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
    /// This class contains all runtime configuration of XmlRcs provider
    /// </summary>
    public class Configuration
    {
        /// <summary>
        /// Convert unix
        /// </summary>
        /// <param name="unixTimeStamp"></param>
        /// <returns></returns>
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
        /// <summary>
        /// Version of XmlRcs
        /// </summary>
        public static readonly Version Version = new System.Version(1, 0, 2, 2);
        /// <summary>
        /// Server of XmlRcs you will need to reconnect the provider in order to apply change to this settings
        /// </summary>
        public static string Server = "huggle-rc.wmflabs.org";
        /// <summary>
        /// Port
        /// </summary>
        public static int Port = 8822;
        /// <summary>
        /// If there is no response from server for this time it will be considered timed out
        /// </summary>
        public static int PingTimeout = 20;
        /// <summary>
        /// The client will send ping requests every PingWait time
        /// </summary>
        public static int PingWait = 10;
    }
}
