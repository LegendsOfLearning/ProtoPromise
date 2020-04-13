using System;
using System.Collections.Generic;

namespace Proto.Promises
{
    partial class Promise
    {
        partial class Internal
        {
            public static Promise _Sequence<TEnumerator>(TEnumerator promiseFuncs, int skipFrames) where TEnumerator : IEnumerator<Func<Promise>>
            {
                ValidateArgument(promiseFuncs, "promiseFuncs", skipFrames + 1);

                if (!promiseFuncs.MoveNext())
                {
                    return Resolved();
                }

                // Invoke funcs async and normalize the progress.
                Promise promise = PromiseVoidResolvePromise0.GetOrCreate(promiseFuncs.Current, skipFrames + 1);
                promise._valueOrPrevious = ResolveContainerVoid.GetOrCreate();
                promise.ResetDepth();
                AddToHandleQueueBack(promise);

                while (promiseFuncs.MoveNext())
                {
                    promise = promise.Then(promiseFuncs.Current);
                }
                return promise.ThenDuplicate(); // Prevents canceling only the very last callback.
            }
        }
    }
}