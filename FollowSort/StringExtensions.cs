using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FollowSort
{
    public static class StringExtensions
    {
        public static string NullIfEmpty(this string s)
        {
            return string.IsNullOrEmpty(s) ? null : s;
        }
    }
}
