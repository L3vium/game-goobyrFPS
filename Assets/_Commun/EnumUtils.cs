using System;
using System.Text.RegularExpressions;

// Author : Baptiste Vu Liem
namespace Com.IsartDigital.Utils
{
    public static class EnumUtils
    {
        public static string EnumToString<T>(T enumValue) where T : Enum
        {
            string lName = enumValue.ToString();
            return Regex.Replace(lName, @"([A-Z])", "$1").ToLower();
        }
    }
}