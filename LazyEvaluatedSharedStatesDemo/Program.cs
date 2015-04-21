using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LazyEvaluatedSharedStatesDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var t = ProcessAsynchronously();
            t.GetAwaiter().GetResult();
            Console.WriteLine("Press ENTER to exit");
            Console.ReadLine();
        }

        static async Task ProcessAsynchronously()
        {
            var unsafeState = new UnsafeState();
            Task[] tasks = new Task[4];
            Console.WriteLine("UnsafeState--unsafe");
            for (int i = 0; i < 4; i++)
            {
                tasks[i] = Task.Run(() => Worker(unsafeState));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("---------------------------------");
            //double check thread safe
            Console.WriteLine("DoubleCheckedLocking--safe");
            var firstState = new DoubleCheckedLocking();
            for (int i = 0; i < 4; i++)
            {
                tasks[i] = Task.Run(() => Worker(firstState));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("---------------------------------");

            //The double-checked pattern is very common, and that is why there are several classes in 
            //the Base Class Library to help us.
            Console.WriteLine("BCLDoubleChecked--safe");
            var secondState = new BCLDoubleChecked();
            for (int i = 0; i < 4; i++)
            {
                tasks[i] = Task.Run(() => Worker(secondState));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("---------------------------------");
            //The most comfortable option is to use the Lazy<T> class that allows us to have thread-safe Lazy-evaluted, shared state.
            Console.WriteLine("Lazy<ValueToAccess>--safe");
            var thirdState = new Lazy<ValueToAccess>(Compute);
            for (int i = 0; i < 4; i++)
            {
                tasks[i] = Task.Run(() => Worker(thirdState));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("---------------------------------");
            Console.WriteLine("BCLThreadSafeFactory--unsafe");

            //The last option is to avoid locking at all, if we do not care about the Construct method.
            //We can just run it several times but use only the first constructed value.
            var fourthState = new BCLThreadSafeFactory();
            for (int i = 0; i < 4; i++)
            {
                tasks[i] = Task.Run(() => Worker(fourthState));
            }
            await Task.WhenAll(tasks);
            Console.WriteLine("---------------------------------");
        }

        static void Worker(IHasValue state)
        {
            Console.WriteLine("Worker runs on thread id {0}",Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("State value: {0}",state.Value.Text);
        }

        static void Worker(Lazy<ValueToAccess> state)
        {
            Console.WriteLine("Worker runs on thread id {0}", Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine("State value: {0}", state.Value.Text);
        }



        static ValueToAccess Compute()
        {
            Console.WriteLine("The value is being constructed on a thread id {0}",Thread.CurrentThread.ManagedThreadId);
            Thread.Sleep(1000);
            return new ValueToAccess(string.Format("Constructed on thread id {0}",Thread.CurrentThread.ManagedThreadId));
        }

        interface IHasValue
        {
            ValueToAccess Value { get; }
        }

        class UnsafeState : IHasValue
        {
            private ValueToAccess _value;

            public ValueToAccess Value
            {
                get
                {
                    if (_value == null)
                    {
                        _value = Compute();
                    }
                    return _value;
                }
            }
        }

        class DoubleCheckedLocking:IHasValue
        {
            private object _syncRoot = new object();
            private volatile ValueToAccess _value;

            public ValueToAccess Value
            {
                get
                {
                    if (_value == null)
                    {
                        lock (_syncRoot)
                        {
                            if (_value == null)
                            {
                                _value = Compute();
                            }
                        }
                    }
                    return _value;
                }
            }
        }

        class BCLDoubleChecked : IHasValue
        {
            private object _syncRoot = new object();
            private ValueToAccess _value;
            private bool _initialized = false;
            public ValueToAccess Value
            {
                get
                {
                    //LazyInitializer.EnsureInitialized method implements the double-checked locking pattern inside.
                    return LazyInitializer.EnsureInitialized(ref _value, ref _initialized, ref _syncRoot, Compute);
                }
            }
        }

        class BCLThreadSafeFactory : IHasValue
        {
            private ValueToAccess _value;
            public ValueToAccess Value
            {
                get
                {
                    return LazyInitializer.EnsureInitialized(ref _value, Compute);
                }
            }
        }

        class ValueToAccess
        {
            private readonly string _text;

            public ValueToAccess(string text)
            {
                _text = text;
            }

            public string Text
            {
                get { return _text; }
            }
        }

    }
}
