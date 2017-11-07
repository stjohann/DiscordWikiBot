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
using System.Threading;
using System.Text;

namespace XmlRcs
{
    class ThreadPool
    {
        private static List<Thread> tp = new List<Thread>();

        public static List<Thread> Threads
        {
            get
            {
                List<Thread> result = new List<Thread>();
                lock (tp)
                    result.AddRange(tp);
                return result;
            }
        }

        public static void UnregisterThis()
        {
            UnregisterThread(Thread.CurrentThread);
        }

        public static void KillThread(Thread thread)
        {
            if (thread == null)
                return;

            if (thread != Thread.CurrentThread)
            {
                if (thread.ThreadState == ThreadState.Running ||
                    thread.ThreadState == ThreadState.WaitSleepJoin ||
                    thread.ThreadState == ThreadState.Background)
                {
                    thread.Abort();
                }
            }
            UnregisterThread(thread);
        }

        public static void RegisterThread(Thread thread)
        {
            if (thread == null)
                return;

            lock (tp)
            {
                if (!tp.Contains(thread))
                {
                    tp.Add(thread);
                }
            }
        }

        public static void UnregisterThread(Thread thread)
        {
            if (thread == null)
                return;

            lock (tp)
            {
                if (tp.Contains(thread))
                    tp.Remove(thread);

            }
        }
    }
}
