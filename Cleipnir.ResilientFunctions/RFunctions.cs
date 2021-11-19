using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cleipnir.ResilientFunctions.Domain;
using Cleipnir.ResilientFunctions.Storage;

namespace Cleipnir.ResilientFunctions
{
    public class RFunctions : IDisposable
    {
        private readonly Dictionary<FunctionTypeId, Delegate> _functions = new();
        private readonly List<IDisposable> _unhandledWatchDogs = new();

        private readonly IFunctionStore _functionStore;
        private readonly SignOfLifeUpdaterFactory _signOfLifeUpdaterFactory;

        private readonly TimeSpan _unhandledWatchDogCheckFrequency;
        private readonly Action<RFunctionException> _unhandledExceptionHandler;

        private readonly object _sync = new();

        private RFunctions(
            IFunctionStore functionStore, 
            SignOfLifeUpdaterFactory signOfLifeUpdaterFactory, 
            TimeSpan unhandledWatchDogCheckFrequency, 
            Action<RFunctionException> unhandledExceptionHandler)
        {
            _functionStore = functionStore;
            _signOfLifeUpdaterFactory = signOfLifeUpdaterFactory;
            _unhandledWatchDogCheckFrequency = unhandledWatchDogCheckFrequency;
            _unhandledExceptionHandler = unhandledExceptionHandler;
        }
        
        public Func<TParam, Task<TReturn>> Register<TParam, TReturn>(
            FunctionTypeId functionTypeId,
            Func<TParam, Task<TReturn>> func,
            Func<TParam, object> idFunc
        ) where TParam : notnull where TReturn : notnull
        {
            lock (_sync)
            {
                if (_functions.ContainsKey(functionTypeId))
                    return (Func<TParam, Task<TReturn>>) _functions[functionTypeId];

                var rFuncRunner = new RFunctionRunner<TParam, TReturn>(
                    functionTypeId,
                    _functionStore,
                    func,
                    idFunc,
                    _signOfLifeUpdaterFactory
                );

                var watchdog = new UnhandledRFunctionWatchdog<TReturn>(
                    functionTypeId,
                    (param1, _, _) => func((TParam) param1),
                    _functionStore,
                    _signOfLifeUpdaterFactory,
                    _unhandledWatchDogCheckFrequency,
                    _unhandledExceptionHandler
                );
                _ = watchdog.Start();

                _unhandledWatchDogs.Add(watchdog);
                
                _functions[functionTypeId] = rFuncRunner.InvokeRFunc;
                return rFuncRunner.InvokeRFunc;
            }
        }

        public Func<TParam1, TParam2, Task<TReturn>> Register<TParam1, TParam2, TReturn>(
            FunctionTypeId functionTypeId,
            Func<TParam1, TParam2, Task<TReturn>> func,
            Func<TParam1, object> idFunc)
            where TParam1 : notnull where TParam2 : notnull where TReturn : notnull
        {
            lock (_sync)
            {
                if (_functions.ContainsKey(functionTypeId))
                    return (Func<TParam1, TParam2, Task<TReturn>>) _functions[functionTypeId];


                var rFuncRunner = new RFunctionRunner<TParam1, TParam2, TReturn>(
                    functionTypeId,
                    _functionStore,
                    func,
                    idFunc,
                    _signOfLifeUpdaterFactory
                );

                var watchdog = new UnhandledRFunctionWatchdog<TReturn>(
                    functionTypeId,
                    (param1, param2, _) => func((TParam1) param1, (TParam2) param2!),
                    _functionStore,
                    _signOfLifeUpdaterFactory,
                    _unhandledWatchDogCheckFrequency,
                    _unhandledExceptionHandler
                );
                _ = watchdog.Start();

                _unhandledWatchDogs.Add(watchdog);

                _functions[functionTypeId] = rFuncRunner.InvokeRFunc;

                return rFuncRunner.InvokeRFunc;
            }
        }

        public Func<TParam, Task<TReturn>> RegisterWithScrapbook<TParam, TScrapbook, TReturn>(
            FunctionTypeId functionTypeId,
            Func<TParam, TScrapbook, Task<TReturn>> func,
            Func<TParam, object> idFunc)
            where TParam : notnull
            where TScrapbook : RScrapbook, new()
            where TReturn : notnull
        {
            lock (_sync)
            {
                //todo consider throwing exception if the method is not equal to the previously registered one...?!
                if (_functions.ContainsKey(functionTypeId))
                    return (Func<TParam, Task<TReturn>>) _functions[functionTypeId];

                var rFuncRunner = new RFunctionRunnerWithScrapbook<TParam, TScrapbook, TReturn>(
                    functionTypeId,
                    _functionStore,
                    func,
                    idFunc,
                    _signOfLifeUpdaterFactory
                );


                var watchdog = new UnhandledRFunctionWatchdog<TReturn>(
                    functionTypeId,
                    (param1, _, scrapbook) => func((TParam) param1, (TScrapbook) scrapbook!),
                    _functionStore,
                    _signOfLifeUpdaterFactory,
                    _unhandledWatchDogCheckFrequency,
                    _unhandledExceptionHandler
                );
                _ = watchdog.Start();

                _unhandledWatchDogs.Add(watchdog);

                _functions[functionTypeId] = new Func<TParam, Task<TReturn>>(rFuncRunner.InvokeRFunc);

                return rFuncRunner.InvokeRFunc;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                foreach (var unhandledWatchDog in _unhandledWatchDogs)
                    unhandledWatchDog.Dispose();

                _unhandledWatchDogs.Clear();
            }
        }

        public static RFunctions Create(
            IFunctionStore store,
            Action<RFunctionException>? unhandledExceptionHandler = null,
            TimeSpan? unhandledFunctionsCheckFrequency = null) 
            => new RFunctions(
                store,
                new SignOfLifeUpdaterFactory(
                    store,
                    unhandledExceptionHandler ?? (_ => {}),
                    unhandledFunctionsCheckFrequency ?? TimeSpan.FromSeconds(10)
                ),
                unhandledFunctionsCheckFrequency ?? TimeSpan.FromSeconds(10),
                unhandledExceptionHandler ?? (_ => {})
            );
    }
}