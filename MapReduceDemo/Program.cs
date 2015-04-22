/****************************************************************************************************
Author:          Xiaoying Wang
DateTime:        2015/4/22 9:33:11
Email Address:   wangxiaoying_op@163.com
CLR Version:     4.0.30319.18444
Machine Name:    WXY-PC
Namespace:       MapReduceDemo
Description:    
 * The Map/Reduce functions are another important parallel programming pattern, It is suitable for a 
 * small program and large multi-server computations. The meaning of this pattern is that you have two
 * special functions to apply to your data. The first of them is the Map function. It takes a set of 
 * initial data in a key/value list form and produces another key/value sequence, transforming the data
 * to the comfortable format for further processing. Then we use another function called Reduce. The Reduce
 * function takes the result of the Map function and transform it to a smallest possible set of data that
 * we actually need.
****************************************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapReduceDemo
{
    class Program
    {
        private static readonly char[] delimiters = Enumerable.Range(0, 256)
            .Select(i => (char)i)
            .Where(c => !char.IsLetterOrDigit(c))
            .ToArray();
        private const string TEXT_TO_PARSE = @"
Call me Ishmael. Some years ago - never mind how long precisely -
having little or no money in my purse, and nothing particular to
interest me on shore, I thought I would sail about a little and
see the watery part of the world. It is a way I have of driving
off the spleen, and regulating the circulation. Whenever I find
myself growing grim about the mouth; whenever it is a damp,
drizzly November in my soul; whenever I find myself involuntarily
pausing before coffin warehouses, and bringing up the rear of
every funeral I meet; and especially whenever my hypos get such
an upper hand of me, that it requires a strong moral principle
to prevent me from deliberately stepping into the street, and
methodically knocking people's hats off - then, I account it high
time to get to sea as soon as I can.
― Herman Melville, Moby Dick.
";
        static void Main(string[] args)
        {
            var q = TEXT_TO_PARSE.Split(delimiters)
                .AsParallel()
                .MapReduce(
                    s => s.ToLower().ToCharArray()
                    , c => c
                    , g => new[] { new { Char = g.Key, Count = g.Count() } })
                .Where(c => char.IsLetterOrDigit(c.Char))
                .OrderByDescending(c => c.Count);
            foreach (var info in q)
            {
                Console.WriteLine("Character {0} occured in the text {1} {2}", info.Char, info.Count, info.Count == 1 ? "time" : "times");
            }
            Console.WriteLine("-----------------------------------------------------");
            const string searchPattern = "en";
            var q2 = TEXT_TO_PARSE.Split(delimiters)
                .AsParallel()
                .Where(s => s.Contains(searchPattern))
                .MapReduce(
                    s => new[] { s },
                    s => s,
                    g => new[] { new { Word = g.Key, Count = g.Count() } })
                .OrderByDescending(s => s.Count);
            Console.WriteLine("Words with search pattern '{0}':", searchPattern);
            foreach (var info in q2)
            {
                Console.WriteLine("{0} occured in the text {1} {2}", info.Word, info.Count, info.Count == 1 ? "time" : "times");
            }

            int halfLengthWordIndex = TEXT_TO_PARSE.IndexOf(' ', TEXT_TO_PARSE.Length / 2);
            using (var sw = File.CreateText("1.txt"))
            {
                sw.Write(TEXT_TO_PARSE.Substring(0, halfLengthWordIndex));
            }
            using (var sw = File.CreateText("2.txt"))
            {
                sw.Write(TEXT_TO_PARSE.Substring(halfLengthWordIndex));
            }
            string[] paths = new[] { ".\\" };
            Console.WriteLine(" ------------------------------------------------");
            var q3 = paths.SelectMany(p => Directory.EnumerateFiles(p, "*.txt"))
                .AsParallel()
                .MapReduce(
                    path => File.ReadLines(path).SelectMany(line =>line.Trim(delimiters).Split(delimiters)),
                    word => string.IsNullOrWhiteSpace(word) ? '\t' :word.ToLower()[0], 
                    g => new[] { new {FirstLetter = g.Key, Count = g.Count()}})
                .Where(s => char.IsLetterOrDigit(s.FirstLetter))
                .OrderByDescending(s => s.Count);
            Console.WriteLine("Words from text files");
            foreach (var info in q3)
            {
                Console.WriteLine("Words starting with letter '{0}' occured in the text {1} {2}",
                    info.FirstLetter,info.Count, info.Count == 1 ? "time" : "times");
            }
            Console.ReadLine();
        }
    }

    static class PLINQExtensions
    {
        public static ParallelQuery<TResult> MapReduce<TSource, TMapped, TKey, TResult>(
            this ParallelQuery<TSource> source,
            Func<TSource, IEnumerable<TMapped>> map,
            Func<TMapped, TKey> keySelector,
            Func<IGrouping<TKey, TMapped>, IEnumerable<TResult>> reduce)
        {
            //We use SelectMany to transform the initial sequence to the sequence we need by applying the 
            //Map function.
            return source.SelectMany(map)
                //Then we choose how we group the new sequence with the keySelector function, and we use
                //GroupBy with this key to produce an intermediate key/value sequence.
                .GroupBy(keySelector)
                .SelectMany(reduce);
        }
    }
}
