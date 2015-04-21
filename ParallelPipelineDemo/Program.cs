/****************************************************************************************************
Author:          Xiaoying Wang
DateTime:        2015/4/21 14:25:10
Email Address:   wangxiaoying_op@163.com
CLR Version:     4.0.30319.18444
Machine Name:    WXY-PC
Namespace:       ParallelPipelineDemo
Description:    
This demo shows how to implement a specific scenario of a producer/consumer pattern, which is called
Parallel Pipeline, using the standard BlockingCollection data structure.
In this demo, we implement one of the most common parallel programming scenarios. Image that we have some
data that has to pass through several computation stages, which take a significant amount of time. The latter
computation requires the results of the former, so we cannot run them in parallel. We can use a Parallel Pipeline technique.
This means that we do not have to wait until all items pass through the first computation stage to go to the next one. It
is enough to have just one item that finishes the stage.
****************************************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelPipelineDemo
{
    class Program
    {
        private const int CollectionsNumber = 4;
        private const int Count = 10;
        static void Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Task.Run(() =>
            {
                if (Console.ReadKey().KeyChar == 'c')
                {
                    cts.Cancel();
                }
            });

            var sourceArrays = new BlockingCollection<int>[CollectionsNumber];
            for (int i = 0; i < sourceArrays.Length; i++)
            {
                sourceArrays[i] = new BlockingCollection<int>(Count);
            }
            var filter1 = new PipelineWorker<int, decimal>(sourceArrays, (n) => Convert.ToDecimal(n * 0.97), cts.Token,
                "filter1");
            var filter2 = new PipelineWorker<decimal, string>(filter1.Output, (s) => string.Format("--{0}--", s),
                cts.Token, "filter2");
            var filter3 = new PipelineWorker<string, string>(filter2.Output,
                (s) =>
                    Console.WriteLine("The final result is {0} on thread id {1}", s,
                        Thread.CurrentThread.ManagedThreadId), cts.Token, "filter3");
            try
            {
                //We run all the stages in parallel, the initial stage runs in parallel as well
                Parallel.Invoke(
                    () =>
                    {
                        Parallel.For(0, sourceArrays.Length * Count,
                            (j, state) =>
                            {
                                if (cts.Token.IsCancellationRequested)
                                {
                                    state.Stop();
                                }
                                int k = BlockingCollection<int>.TryAddToAny(sourceArrays, j);
                                if (k >= 0)
                                {
                                    Console.WriteLine("added {0} to source data on thread id {1}", j,
                                        Thread.CurrentThread.ManagedThreadId);
                                    Thread.Sleep(100);
                                }
                            });
                        foreach (var arr in sourceArrays)
                        {
                            arr.CompleteAdding();
                        }
                    }, 
                    filter1.Run,
                    filter2.Run,
                    filter3.Run);
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                {
                    Console.WriteLine(ex.Message + ex.StackTrace);
                }
            }

            if (cts.Token.IsCancellationRequested)
            {
                Console.WriteLine("Operation has been canceled! Press ENTER to exit.");
            }
            else
            {
                Console.WriteLine("Press ENTER to exit.");
            }
            Console.ReadLine();
        }
    }

    class PipelineWorker<TInput, TOutput>
    {
        private const int Count = 10;
        private Func<TInput, TOutput> _processor = null;
        private Action<TInput> _outputProcessor = null;
        private BlockingCollection<TInput>[] _input = null;
        private CancellationToken _token ;

        public PipelineWorker(BlockingCollection<TInput>[] input, Func<TInput, TOutput> processor,
            CancellationToken token, string name)
        {
            _input = input;
            Output = new BlockingCollection<TOutput>[_input.Length];
            for (int i = 0; i < Output.Length; i++)
            {
                Output[i] = null == input[i] ? null : new BlockingCollection<TOutput>(Count);
            }
            _processor = processor;
            _token = token;
            Name = name;
        }

        public PipelineWorker(BlockingCollection<TInput>[] input, Action<TInput> renderer,
            CancellationToken token, string name)
        {
            _input = input;
            _outputProcessor = renderer;
            _token = token;
            Name = name;
            Output = null;
        }

        public BlockingCollection<TOutput>[] Output { get; private set; }

        public string Name { get; private set; }

        public void Run()
        {
            Console.WriteLine("{0} is running", this.Name);
            while (!_input.All(bc => bc.IsCompleted) && !_token.IsCancellationRequested)
            {
                TInput receivedItem;
                int i = BlockingCollection<TInput>.TryTakeFromAny(_input, out receivedItem, 50, _token);
                if (i >= 0)
                {
                    if (Output != null)
                    {
                        TOutput outputItem = _processor(receivedItem);
                        BlockingCollection<TOutput>.AddToAny(Output, outputItem);
                        Console.WriteLine("{0} sent {1} to next, on thread id {2}", Name, outputItem,
                            Thread.CurrentThread.ManagedThreadId);
                        Thread.Sleep(100);
                    }
                    else
                    {
                        _outputProcessor(receivedItem);
                    }
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
            if (Output != null)
            {
                foreach (var bc in Output)
                {
                    bc.CompleteAdding();
                }
            }
        }
    }
}
