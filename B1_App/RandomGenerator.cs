using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B1_App
{
    public static class RandomGenerator
    {
        public static string EngString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        }

        public static string RusString(int length)
        {
            const string chars = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Shared.Next(s.Length)]).ToArray());
        }

        public static int EvenIntNumber(int minValue, int maxValue)
        {
            int rndValue;
            do
                rndValue = Random.Shared.Next(minValue, maxValue);
            while (rndValue % 2 != 0);
            return rndValue;
        }

        public static double DoubleNumber(double minValue, double maxValue)
        {
            return Math.Round(minValue + Random.Shared.NextDouble() * (maxValue - minValue + double.Epsilon), 8);
        }

        public static DateTime DateBetween(DateTime previous, DateTime next)
        {
            int difference = (int)((next.Ticks - previous.Ticks) / TimeSpan.TicksPerSecond);
            return new DateTime(next.Ticks - (Random.Shared.Next(0, difference) * TimeSpan.TicksPerSecond));
        }
    }
}
