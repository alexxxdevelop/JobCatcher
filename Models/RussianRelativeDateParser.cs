using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JobCatcher
{
    public class RussianRelativeDateParser
    {
        private static readonly Dictionary<string, int> Months = new Dictionary<string, int>
        {
            ["января"] = 1,
            ["февраля"] = 2,
            ["марта"] = 3,
            ["апреля"] = 4,
            ["мая"] = 5,
            ["июня"] = 6,
            ["июля"] = 7,
            ["августа"] = 8,
            ["сентября"] = 9,
            ["октября"] = 10,
            ["ноября"] = 11,
            ["декабря"] = 12
        };

        public static DateTime Parse(string input, DateTime? relativeTo = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return DateTime.Now;

            var now = relativeTo ?? DateTime.Now;
            input = input.Trim().ToLower();

            // "1 июня, 00:20"
            var dateMatch = Regex.Match(input,
                @"^(\d{1,2})\s+(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря),?\s+(\d{1,2}):(\d{2})$");

            if (dateMatch.Success)
            {
                int day = int.Parse(dateMatch.Groups[1].Value);
                int month = Months[dateMatch.Groups[2].Value];
                int hour = int.Parse(dateMatch.Groups[3].Value);
                int minute = int.Parse(dateMatch.Groups[4].Value);

                int year = now.Year;
                // Если дата в будущем, берем предыдущий год
                if (new DateTime(year, month, day) > now)
                    year--;

                return new DateTime(year, month, day, hour, minute, 0);
            }

            // "30 минут назад"
            var minutesMatch = Regex.Match(input, @"^(\d+)\s+минут(у|ы)?\s+назад$");
            if (minutesMatch.Success)
            {
                int minutes = int.Parse(minutesMatch.Groups[1].Value);
                return now.AddMinutes(-minutes);
            }

            // "X часов Y минут назад"
            var hoursMinutesMatch = Regex.Match(input,
                @"^(\d+)\s+час(?:ов|а)?\s+(\d+)\s+минут(?:у|ы)?\s+назад$");
            if (hoursMinutesMatch.Success)
            {
                int hours = int.Parse(hoursMinutesMatch.Groups[1].Value);
                int minutes = int.Parse(hoursMinutesMatch.Groups[2].Value);
                return now.AddHours(-hours).AddMinutes(-minutes);
            }

            // "X часов назад"
            var hoursMatch = Regex.Match(input, @"^(\d+)\s+час(?:ов|а)?\s+назад$");
            if (hoursMatch.Success)
            {
                int hours = int.Parse(hoursMatch.Groups[1].Value);
                return now.AddHours(-hours);
            }

            return DateTime.Now;
        }
    }
}
