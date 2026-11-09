namespace RaceDay.Mvc.Services
{
    public static class Ordinal
    {
        // 1 -> "1st", 2 -> "2nd", 3 -> "3rd", 11 -> "11th", 47 -> "47th"
        public static string Of(int number)
        {
            var lastTwo = number % 100;
            if (lastTwo is >= 11 and <= 13)
                return number + "th";

            return (number % 10) switch
            {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th"
            };
        }
    }
}
