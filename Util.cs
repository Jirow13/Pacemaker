﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;

namespace CampaignPacer
{
	public class Util
	{
		public static bool EnableLog
		{
			get
			{
				return Log is GameLog; // GameLog, derived from GameLogBase, provides thread-safe, async logging & in-game text display
			}
			set
			{
				if (Log is GameLog && !value)
					Log = new GameLogBase();
				else if (!(Log is GameLog) && value)
					Log = new GameLog(Main.Name, truncate: true, logName: "debug");
			}
		}

		public static bool EnableTracer { get; set; } = false;

		public static GameLogBase Log = new GameLogBase(); // GameLogBase, parent of GameLog, implements do-nothing virtual output methods

		public static class EventTracer
		{
			private static readonly ConcurrentDictionary<string, bool> _stackTraceMap = new ConcurrentDictionary<string, bool>();

			[MethodImpl(MethodImplOptions.NoInlining)]
			public static void Trace(List<string> extraInfo = null)
			{
				if (!EnableTracer || !EnableLog)
					return;

				var st = new StackTrace(1, true);
				var frames = st.GetFrames();
				var evtMethod = frames[0].GetMethod();

				var msg = new List<string>
				{
					$"Code Event Invoked: {evtMethod.DeclaringType}.{evtMethod.Name}",
					$"Real Timestamp:     {DateTime.Now:MM/dd H:mm:ss.fff}",
				};

				if (Campaign.Current != null)
				{
					msg.AddRange(new List<string>
					{
						$"Campaign Time:      {CampaignTime.Now}",
						$"  In Seasons:       {CampaignTime.Now.ElapsedSeasonsUntilNow}",
						$"  In Weeks:         {CampaignTime.Now.ElapsedWeeksUntilNow}",
						$"  In Days:          {CampaignTime.Now.ElapsedDaysUntilNow}",
						$"  In Hours:         {CampaignTime.Now.ElapsedHoursUntilNow}",
						$"  Week of Season:   {CampaignTime.Now.GetWeekOfSeason}",
						$"  Day of Week:      {CampaignTime.Now.GetDayOfWeek}",
						$"  Hour of Day:      {CampaignTime.Now.GetHourOfDay}",
					});
				}

				var stStr = st.ToString();

				if (stStr.Length > 2)
				{
					// ensure we're using Unix-style EOLs in the stack trace & remove extra newline at end
					stStr = stStr.Replace("\r\n", "\n").Remove(stStr.Length - 1, 1);

					if (_stackTraceMap.TryAdd(stStr, true))
					{
						msg.AddRange(new List<string>
						{
							String.Empty,
							"Stack Trace:",
							stStr,
						});
					}
				}

				if (extraInfo != null && extraInfo.Count > 0)
				{
					msg.AddRange(new List<string>
					{
						String.Empty,
						"Extra Information:",
					});

					if (extraInfo.Count > 1)
						msg.Add(String.Empty);

					msg.AddRange(extraInfo);
				}

				Log.ToFile(msg, true);
			}
		}
	}
}