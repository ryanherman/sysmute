﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace sysmute
{
    internal static class DateTimeExt
    {
        // DateTime extention to easily compare 2 times
        // http://stackoverflow.com/questions/10631044/c-sharp-compare-time-between-two-time-intervals
        public static bool TimeOfDayIsBetween(this DateTime t, DateTime start, DateTime end)
        {
            var time_of_day = t.TimeOfDay;
            var start_time_of_day = start.TimeOfDay;
            var end_time_of_day = end.TimeOfDay;

            if (start_time_of_day <= end_time_of_day)
                return start_time_of_day <= time_of_day && time_of_day <= end_time_of_day;

            return start_time_of_day <= time_of_day || time_of_day <= end_time_of_day;
        }

        public static int Hour(this string timeString)
        {
            try
            {
                var hour = Int32.Parse(timeString.Substring(0, timeString.IndexOf(":", StringComparison.Ordinal)));

                return hour;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid hour {timeString}.  Must use HH:MM");
                throw e;
            }
        }

        public static int Minute(this string timeString)
        {
            try
            {
                var minute = Int32.Parse(timeString.Substring(timeString.IndexOf(":", StringComparison.Ordinal) + 1));

                return minute;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invalid minute {timeString}.  Must use HH:MM");
                throw e;
            }
        }
    }

    internal class Program
    {
        public static DateTime startTime = new DateTime(1969, 2, 25, 22, 00, 00); // Date doesn't matter. 10pm
        public static DateTime endTime = new DateTime(1969, 2, 26, 9, 00, 00); // Date doesn't matter. 9am
        public static int mouseIdleTime = 5; // Time mouse doesn't move to be considered idle in minutes
        public static readonly int SleepInterval = 1000 * 60; // Check the time every 1 minute

        private static void muteVolume()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Muting Master Volume");
            Console.ForegroundColor = ConsoleColor.White;
            AudioManager.SetMasterVolumeMute(true);
        }

        private static void unmuteVolume()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Unmuting Master Volume");
            Console.ForegroundColor = ConsoleColor.White;
            AudioManager.SetMasterVolumeMute(false);
        }

        public static void AddSystemIcon()
        {
            // Try to add notifications
            Thread notifyThread = new Thread(
                delegate()
                {
                    NotifyIcon trayIcon = new NotifyIcon();
                    trayIcon.Text = "Sysmute";
                    trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

                    ContextMenu trayMenu = new ContextMenu();

                    trayMenu.MenuItems.Add("&Mute", (sender, eventArgs) =>
                    {
                        muteVolume();
                    });

                    trayMenu.MenuItems.Add("&Unmute", (sender, eventArgs) =>
                    {
                        unmuteVolume();
                    });

                    trayMenu.MenuItems.Add("-", (sender, eventArgs) =>
                    {
                        unmuteVolume();
                    });

                    trayMenu.MenuItems.Add("&About", (sender, eventArgs) =>
                    {
                        Process.Start("https://brettmorrison.com/");
                    });

                    trayIcon.ContextMenu = trayMenu;
                    trayIcon.Visible = true;
                    Application.Run();
                    Console.WriteLine("Done");
                });

            notifyThread.Start();
        }

        private static void Main(string[] args)
        {
            // Check the command args.  If set, override the default
            if (args.Length > 0 && args[0] != null)
            {
                try
                {
                    startTime = new DateTime(1969, 2, 25, args[0].Hour(), args[0].Minute(), 00);
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid start time");
                    return;
                }
            }

            if (args.Length > 1 && args[1] != null)
            {
                try
                {
                    endTime = new DateTime(1969, 2, 26, args[1].Hour(), args[1].Minute(), 00);
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid end time");
                    return;
                }
            }

            if (args.Length > 2 && args[2] != null)
            {
                try
                {
                    mouseIdleTime = int.Parse(args[2]);
                }
                catch (Exception)
                {
                    Console.WriteLine("Invalid mouse idle time");
                    return;
                }
            }

            // Add System Icon
            AddSystemIcon();

            Console.WriteLine($"sysmute. Program will mute system audio between {startTime.TimeOfDay} and {endTime.TimeOfDay} and check for mouse input every {mouseIdleTime} minutes");
            if (args.Length == 0)
                Console.WriteLine($"To override startTime, endTime and mouseIdleTime minutes, pass in via command line. E.g. > sysmute 23:00 08:00 5");

            var LastX = (uint)0;
            var LastY = (uint)0;
            var MouseIdleTimer = new Stopwatch();

            while (true)
            {
                var timeNow = DateTime.Now;
                Console.WriteLine($"Current Time: {timeNow.TimeOfDay}");

                // If the current time falls within the range of the quiet time, mute
                // If user unmutes during quiet time, it will re-mute after a period of idle time
                if (timeNow.TimeOfDayIsBetween(startTime, endTime))
                {
                    // Audio isn't muted, check mouse for idle to ensure we want to mute
                    if (!AudioManager.GetMasterVolumeMute())
                    {
                        var CurrentX = (uint)Cursor.Position.X;
                        var CurrentY = (uint)Cursor.Position.Y;

                        if (!MouseIdleTimer.IsRunning)
                        {
                            Console.WriteLine($"Starting timer to check for mouse activity every {mouseIdleTime} minutes");
                            MouseIdleTimer.Start();
                            LastX = CurrentX;
                            LastY = CurrentY;
                        }
                        else
                        {
                            if (CurrentX == LastX && CurrentY == LastY)
                            {
                                if (MouseIdleTimer.Elapsed.Minutes >= mouseIdleTime)
                                {
                                    Console.WriteLine($"User is idle.  Mouse hasn't moved in > {mouseIdleTime} minutes");
                                    muteVolume();
                                    MouseIdleTimer.Stop();
                                }
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Detected Movement -> Restarting idle mouse timer and coordinates.");
                                Console.ForegroundColor = ConsoleColor.White;
                                MouseIdleTimer.Restart();
                                LastX = CurrentX;
                                LastY = CurrentY;
                            }
                        }
                    }
                    else
                    {
                        // Edge condition. Sound is muted, just check and ensure the mouse idle timer resets properly
                        if (MouseIdleTimer.IsRunning)
                        {
                            Console.WriteLine("Stopping idle mouse timer.");
                            MouseIdleTimer.Stop();
                        }
                    }
                }

                Thread.Sleep(SleepInterval);
            }
        }
    }
}