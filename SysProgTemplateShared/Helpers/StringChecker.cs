using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysProgTemplateShared.Helpers
{
    public static class StringChecker
    {
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            // Остальные символы - буквы или цифры
            return name.All(c => IsLatinLetter(c) || IsDigit(c));
        }

        public static bool IsLatinLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        public static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        public static bool IsLatinLetterOrDigitOrUnderscore(char c)
        {
            return IsLatinLetter(c) || IsDigit(c) || c == '_';
        }
    }
}
