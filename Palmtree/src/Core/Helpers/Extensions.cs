/**
 * The Extensions class
 * 
 * ...
 * 
 * 
 * Copyright (C) 2017:  RIBS group (Nick Ramsey Lab), University Medical Center Utrecht (The Netherlands) & external contributors
 * Author(s):           Max van den Boom            (info@maxvandenboom.nl)
 *                      Benny van der Vijgh         (benny@vdvijgh.nl)
 * 
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software
 * Foundation, either version 3 of the License, or (at your option) any later version. This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
 * more details. You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.
 */
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Palmtree.Core.Helpers {


    /// <summary>
    /// The <c>Extensions</c> class.
    /// 
    /// Abc.
    /// </summary>
    public static class Extensions {

        private const string CLASS_NAME = "extensions";
        private static Logger logger = LogManager.GetLogger(CLASS_NAME);           

        // TODO: use non-Linq statement?
        public static int[] findIndices(int[] array, int match) {
            return Enumerable.Range(0, array.Length)
                         .Where(i => array[i] == match)
                         .ToArray();
        }

        public static int[] unique(this int[] arr) {
            HashSet<int> set = new HashSet<int>(arr);
            return set.ToArray<int>();
        }

        // convert one and two-dimensional arrays to string. If given 0-dimensional input (e.g. double), generic ToString() method is used if object has this method
        public static string arrayToString(dynamic arr) {

            string output = "";

            if (arr != null) {
                if (arr.GetType().Name == "Double[]") { foreach (var item in arr) { output += item + ", "; } } 
                else if (arr.GetType().Name == "Double[][]") { foreach (var item in arr) { foreach (var subItem in item) { output += subItem + ", "; } } } 
                else {
                    try {
                        if (arr.GetType().GetMethod("ToString") != null) output = arr.ToString();
                        else logger.Error("Not able to cast object to String, check if object is array.");
                    } catch (System.Reflection.AmbiguousMatchException) { output = arr.ToString(); }            // if there are overloads of ToString, an Ambigous Exception is thrown, but this means there is a method we can use, so call method
                }
            } else {
                logger.Error("Null object given to cast to string, empty String returned.");
            }

            return output;
        }

        public static void Swap<T>(this IList<T> list, int indexA, int indexB) {
            T tmp = list[indexA];
            list[indexA] = list[indexB];
            list[indexB] = tmp;
        }

        public static void Shuffle<T>(this IList<T> list) {

            int n = list.Count;
            while (n > 1) {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }

        }


        public static string TrimAll(this string str) {
            var len = str.Length;
            var src = str.ToCharArray();
            int dstIdx = 0;
            for (int i = 0; i < len; i++) {
                var ch = src[i];
                switch (ch) {
                    case '\u0020': case '\u00A0': case '\u1680': case '\u2000': case '\u2001': 
                    case '\u2002': case '\u2003': case '\u2004': case '\u2005': case '\u2006': 
                    case '\u2007': case '\u2008': case '\u2009': case '\u200A': case '\u202F': 
                    case '\u205F': case '\u3000': case '\u2028': case '\u2029': case '\u0009': 
                    case '\u000A': case '\u000B': case '\u000C': case '\u000D': case '\u0085':
                        continue;
                    default:
                        src[dstIdx++] = ch;
                        break;
                }
            }
            return new string(src, 0, dstIdx);
        }

    }

    static class ThreadSafeRandom {

        [ThreadStatic]
        private static Random Local;

        public static Random ThisThreadsRandom {
            get { return Local ?? (Local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId))); }
        }

    }

}
