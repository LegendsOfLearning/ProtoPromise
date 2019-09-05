﻿// undef CANCEL to disable, or define CANCEL to enable cancelations on promises.
// If CANCEL is defined, it breaks the Promises/A+ spec "2.1. Promise States", but allows breaking promise chains.
#define CANCEL
// undef PROGRESS to disable, or define PROGRESS to enable progress reports on promises.
// If PROGRESS is defined, promises use more memory. If PROGRESS is undefined, there is no limit to the depth of a promise chain.
#define PROGRESS
// define DEBUG to enable debugging options in RELEASE mode. undef DEBUG to disable debugging options in DEBUG mode.
//#define DEBUG

#pragma warning disable IDE0018 // Inline variable declaration
#pragma warning disable IDE0034 // Simplify 'default' expression
#pragma warning disable CS0618 // Type or member is obsolete
#pragma warning disable RECS0029 // Warns about property or indexer setters and event adders or removers that do not use the value parameter
using System;

namespace ProtoPromise
{
    partial class Promise
    {
        public enum State : byte
        {
            Pending,
            Resolved,
            Rejected,
#if !CANCEL
            [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", false)]
#endif
            Canceled // This violates Promises/A+ 2.1 when CANCEL is enabled.
        }

        public enum GeneratedStacktrace : byte
        {
            /// <summary>
            /// Don't generate any extra stack traces.
            /// </summary>
            None,
            /// <summary>
            /// Generate stack traces when Deferred.Reject is called.
            /// If Reject is called with an exception, the generated stack trace is appended to the exception's stacktrace.
            /// </summary>
            Rejections,
            /// <summary>
            /// Generate stack traces when Deferred.Reject is called.
            /// Also generate stack traces every time a promise is created (i.e. with .Then). This can help debug where an invalid object was returned from a .Then delegate.
            /// If a .Then/.Catch callback throws an exception, the generated stack trace is appended to the exception's stacktrace.
            /// <para/>
            /// NOTE: This can be extremely expensive, so you should only enable this if you ran into an error and you are not sure where it came from.
            /// </summary>
            All
        }

        public enum PoolType : byte
        {
            /// <summary>
            /// Don't pool any objects.
            /// </summary>
            None,
            /// <summary>
            /// Only pool internal objects.
            /// </summary>
            Internal,
            /// <summary>
            /// Pool all objects, internal and public.
            /// </summary>
            All
        }

        public static class Config
        {
            /// <summary>
            /// If you need to support more whole numbers (longer promise chains), decrease decimalBits. If you need higher precision, increase decimalBits.
            /// <para/>
            /// Max Whole Number: 2^(32-<see cref="ProgressDecimalBits"/>)
            /// Precision: 1/(2^<see cref="ProgressDecimalBits"/>)
            /// Don't make this smaller than 8 since the maximum contiguous integer representable in a float is 2^24
            /// <para/>
            /// NOTE: promises that don't wait (.Then with an onResolved that simply returns a value or void) don't count towards the promise chain limit.
            /// </summary>
            public const int ProgressDecimalBits = 13;

            private static PoolType _objectPooling = PoolType.Internal;
            /// <summary>
            /// Highly recommend to leave this None or Internal in DEBUG mode, so that exceptions will propagate if/when promises are used incorrectly after they have already completed.
            /// </summary>
            public static PoolType ObjectPooling { get { return _objectPooling; } set { _objectPooling = value; } }

#if DEBUG
            public static GeneratedStacktrace DebugStacktraceGenerator { get; set; }
#else
            public static GeneratedStacktrace DebugStacktraceGenerator { get { return default(GeneratedStacktrace); } set { } }
#endif
        }

        partial class DeferredBase
        {
            /// <summary>
            /// Report progress between 0 and 1.
            /// </summary>
#if !PROGRESS
            [Obsolete("Define PROGRESS in ProtoPromise/Config.cs to enable progress reports.", true)]
#endif
            public abstract void ReportProgress(float progress);

#if !CANCEL
            [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", true)]
#endif

            public void Cancel()
            {
                ValidateCancel();
                var promise = Promise;
                ValidateOperation(promise);

                if (State == State.Pending)
                {
                    promise.Release();
                }
                else
                {
                    Logger.LogWarning("Deferred.Cancel - Deferred is not in the pending state.");
                    return;
                }

                State = State.Canceled;
                promise.Cancel();
            }

#if !CANCEL
            [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", true)]
#endif
            public void Cancel<TCancel>(TCancel reason)
            {
                ValidateCancel();
                var promise = Promise;
                ValidateOperation(promise);

                if (State == State.Pending)
                {
                    promise.Release();
                }
                else
                {
                    Logger.LogWarning("Deferred.Cancel - Deferred is not in the pending state.");
                    return;
                }

                State = State.Canceled;
                promise.Cancel(reason);
            }
        }

#if !PROGRESS
        [Obsolete("Define PROGRESS in ProtoPromise/Config.cs to enable progress reports.", true)]
#endif
        public Promise Progress(Action<float> onProgress)
        {
            ValidateProgress();
#if PROGRESS
            ProgressInternal(onProgress, 1);
#endif
            return this;
        }

        // TODO: Return a different chainable object (CancelCatch?)
#if !CANCEL
        [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", true)]
#endif
        public Promise Canceled(Action onCanceled)
        {
            AddCancelDelegate(onCanceled, 1);
            return this;
        }

#if !CANCEL
        [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", true)]
#endif
        public Promise Canceled<TCancel>(Action<TCancel> onCanceled)
        {
            AddCancelDelegate(onCanceled, 1);
            return this;
        }

        protected void AddCancelDelegate(Action onCanceled, int skipFrames)
        {
            ValidateCancel();
            ValidateOperation(this);
            ValidateArgument(onCanceled, "onCanceled");

            AddWaiter(Internal.CancelDelegate.GetOrCreate(onCanceled, 1));
        }

        protected void AddCancelDelegate<TCancel>(Action<TCancel> onCanceled, int skipFrames)
        {
            ValidateCancel();
            ValidateOperation(this);
            ValidateArgument(onCanceled, "onCanceled");

            AddWaiter(Internal.CancelDelegate<TCancel>.GetOrCreate(onCanceled, 1));
        }

        /// <summary>
        /// Cancels this promise and all promises that have been chained from this.
        /// Does nothing if this promise isn't pending.
        /// </summary>
#if !CANCEL
        [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", true)]
#endif
        public void Cancel()
        {
            ValidateCancel();
            ValidateOperation(this);

            if (_state != State.Pending)
            {
                return;
            }

            _state = State.Canceled;

            _rejectedOrCanceledValue = Internal.CancelVoid.GetOrCreate();
            _rejectedOrCanceledValue.Retain();

            CancelProgressListeners();
            OnCancel();
        }

        /// <summary>
        /// Cancels this promise and all promises that have been chained from this with the provided cancel reason.
        /// Does nothing if this promise isn't pending.
        /// </summary>
#if !CANCEL
        [Obsolete("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.", true)]
#endif
        public void Cancel<TCancel>(TCancel reason)
        {
            ValidateCancel();
            ValidateOperation(this);

            if (_state != State.Pending)
            {
                return;
            }

            _state = State.Canceled;

            _rejectedOrCanceledValue = Internal.CancelValue<TCancel>.GetOrCreate(reason);
            _rejectedOrCanceledValue.Retain();

            CancelProgressListeners();
            OnCancel();
        }



#if CSHARP_7_3_OR_NEWER // Really C# 7.2, but this symbol is the closest Unity offers.
        private
#endif
        protected Promise()
        {
#if DEBUG
            _id = idCounter++;
#endif
        }

#if DEBUG || PROGRESS
        private Promise _previous;
#endif

        protected static void _SetStackTraceFromCreated(Internal.IStacktraceable stacktraceable, Internal.UnhandledExceptionInternal unhandledException)
        {
            SetStackTraceFromCreated(stacktraceable, unhandledException);
        }

        // Calls to these get compiled away in RELEASE mode
        static partial void ValidateOperation(Promise promise);
        static partial void ValidateProgress(float progress);
        static partial void ValidateArgument(Delegate del, string argName);
        partial void ValidateReturn(Promise other);
        static partial void ValidateReturn(Delegate other);

        static partial void SetCreatedStackTrace(Internal.IStacktraceable stacktraceable, int skipFrames);
        static partial void SetStackTraceFromCreated(Internal.IStacktraceable stacktraceable, Internal.UnhandledExceptionInternal unhandledException);
        static partial void SetRejectStackTrace(Internal.UnhandledExceptionInternal unhandledException, int skipFrames);
        partial void SetNotDisposed();
#if DEBUG
        private string _createdStackTrace;
        string Internal.IStacktraceable.Stacktrace { get { return _createdStackTrace; } set { _createdStackTrace = value; } }

        private static int idCounter;
        protected readonly int _id;

        private void SetDisposed()
        {
            _rejectedOrCanceledValue = Internal.DisposedChecker.instance;
        }

        partial void SetNotDisposed()
        {
            _rejectedOrCanceledValue = null;
        }

        partial class Internal
        {
            // This allows us to re-use the reference field without having to add another bool field.
            public sealed class DisposedChecker : IValueContainer
            {
                public static readonly DisposedChecker instance = new DisposedChecker();

                private DisposedChecker() { }

                void IValueContainer.Release() { throw new InvalidOperationException(); }

                void IValueContainer.Retain() { throw new InvalidOperationException(); }

                bool IValueContainer.TryGetValueAs<U>(out U value) { throw new InvalidOperationException(); }
            }
        }

        static partial void SetCreatedStackTrace(Internal.IStacktraceable stacktraceable, int skipFrames)
        {
            if (Config.DebugStacktraceGenerator == GeneratedStacktrace.All)
            {
                stacktraceable.Stacktrace = GetStackTrace(skipFrames + 1);
            }
        }

        static partial void SetStackTraceFromCreated(Internal.IStacktraceable stacktraceable, Internal.UnhandledExceptionInternal unhandledException)
        {
            unhandledException.SetStackTrace(FormatStackTrace(stacktraceable.Stacktrace));
        }

        static partial void SetRejectStackTrace(Internal.UnhandledExceptionInternal unhandledException, int skipFrames)
        {
            if (Config.DebugStacktraceGenerator != GeneratedStacktrace.None)
            {
                unhandledException.SetStackTrace(FormatStackTrace(GetStackTrace(skipFrames + 1)));
            }
        }

        private static string GetStackTrace(int skipFrames)
        {
            return new System.Diagnostics.StackTrace(skipFrames + 1, true).ToString();
        }

        private static System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder(128);

        private static string FormatStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
            {
                return stackTrace;
            }

            stringBuilder.Length = 0;
            stringBuilder.Append(stackTrace);

            // Format stacktrace to match "throw exception" so that double-clicking log in Unity console will go to the proper line.
            return stringBuilder.Remove(0, 1)
                .Replace(":line ", ":")
                .Replace("\n ", " \n")
                .Replace("(", " (")
                .Replace(") in", ") [0x00000] in") // Not sure what "[0x00000]" is, but it's necessary for Unity's parsing.
                .Append(" ")
                .ToString();
        }

        partial void ValidateReturn(Promise other)
        {
            if (other == null)
            {
                // Returning a null from the callback is not allowed.
                throw new InvalidReturnException("A null promise was returned.");
            }

            // Validate returned promise as not disposed.
            try
            {
                ValidateOperation(other);
            }
            catch (ObjectDisposedException e)
            {
                throw new InvalidReturnException("A disposed promise was returned.", innerException: e);
            }

            // TODO: handle checking through AllPromise.
            // A promise cannot wait on itself.
            for (var prev = other; prev != null; prev = prev._previous)
            {
                if (prev == this)
                {
                    throw new InvalidReturnException("Circular Promise chain detected.", other._createdStackTrace);
                }
            }
        }

        static partial void ValidateReturn(Delegate other)
        {
            if (other == null)
            {
                // Returning a null from the callback is not allowed.
                throw new InvalidReturnException("A null delegate was returned.");
            }
        }

        static protected void ValidateProgressValue(float value)
        {
            const string argName = "progress";
            if (value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(argName, "Must be between 0 and 1.");
            }
        }

        protected void ValidateNotDisposed()
        {
            if (ReferenceEquals(_rejectedOrCanceledValue, Internal.DisposedChecker.instance))
            {
                throw new ObjectDisposedException("Always nullify your references when you are finished with them!" +
                    " Call Retain() if you want to perform operations after the promise has finished. Remember to call Release() when you are finished with it!");
            }
        }

        static partial void ValidateOperation(Promise promise)
        {
            promise.ValidateNotDisposed();
        }

        static partial void ValidateProgress(float progress)
        {
            ValidateProgressValue(progress);
        }

        static protected void ValidateArg(Delegate del, string argName)
        {
            if (del == null)
            {
                throw new ArgumentNullException(argName);
            }
        }

        static partial void ValidateArgument(Delegate del, string argName)
        {
            ValidateArg(del, argName);
        }

        public override string ToString()
        {
            return string.Format("Type: Promise, Id: {0}, State: {1}", _id, _state);
        }
#else
        private void SetDisposed()
        {
            // Allow GC to clean up the object if necessary.
            _rejectedOrCanceledValue = null;
        }

        public override string ToString()
        {
            return string.Format("Type: Promise, State: {0}", _state);
        }
#endif


        // Calls to this get compiled away when CANCEL is defined.
        static partial void ValidateCancel();
        static partial void HandleCanceled();
#if CANCEL
        // Cancel promises in a breadth-first manner.
        private static ValueLinkedQueue<Internal.ITreeHandleable> _cancelQueue;

        private static void AddToCancelQueue(Internal.ITreeHandleable cancelation)
        {
            _cancelQueue.Enqueue(cancelation);
        }

        private static void AddToCancelQueueRisky(Internal.ITreeHandleable cancelation)
        {
            _cancelQueue.EnqueueRisky(cancelation);
        }

        static partial void HandleCanceled()
        {
            while (_handleQueue.IsNotEmpty)
            {
                _handleQueue.DequeueRisky().Cancel();
            }
            _cancelQueue.ClearLast();
        }
#else
        static protected void ThrowCancelException()
        {
            throw new InvalidOperationException("Define CANCEL in ProtoPromise/Config.cs to enable cancelations.");
        }

        static partial void ValidateCancel()
        {
            ThrowCancelException();
        }
#endif

        private void WaitFor(Promise other)
        {
            ValidateReturn(other);
#if CANCEL
            if (_state == State.Canceled)
            {
                // Don't wait for anything if this promise was canceled during the callback, just place in the handle queue so it can be repooled.
                AddToHandleQueue(this);
            }
            else
#endif
            {
                SetPreviousTo(other);
                other.AddWaiter(this);
            }
            HandleCanceled();
        }


        partial class Internal
        {
            public interface IStacktraceable
            {
#if DEBUG
                string Stacktrace { get; set; }
#endif
            }

#pragma warning disable RECS0001 // Class is declared partial but has only one part
            public abstract partial class PromiseWaitDeferred<TPromise> : PoolablePromise<TPromise> where TPromise : PromiseWaitDeferred<TPromise>
            {
                protected readonly DeferredInternal _deferredInternal;
                public new Deferred Deferred { get { return _deferredInternal; } }

                protected PromiseWaitDeferred()
                {
                    _deferredInternal = new DeferredInternal(this);
                }

                protected override void Reset(int skipFrames)
                {
                    _deferredInternal.Reset();
                    // Retain now, release when deferred resolves/rejects/cancels.
                    Retain();
                    base.Reset(skipFrames + 1);
                }
            }

            public abstract partial class PromiseWaitDeferred<T, TPromise> : PoolablePromise<T, TPromise> where TPromise : PromiseWaitDeferred<T, TPromise>
            {
                protected readonly Internal.DeferredInternal _deferredInternal;
                public new Deferred Deferred { get { return _deferredInternal; } }

                protected PromiseWaitDeferred()
                {
                    _deferredInternal = new Internal.DeferredInternal(this);
                }

                protected override void Reset(int skipFrames)
                {
                    _deferredInternal.Reset();
                    // Retain now, release when deferred resolves/rejects/cancels.
                    Retain();
                    base.Reset(skipFrames + 1);
                }
            }
#pragma warning restore RECS0001 // Class is declared partial but has only one part
        }

        // Calls to these get compiled away when PROGRESS is undefined.
        partial void SetDepthAndPrevious(Promise next);
        partial void ClearPrevious();
        partial void ResetDepth();

        partial void ClearProgressListeners();
        partial void ResolveProgressListeners();
        partial void RejectProgressListeners();
        partial void CancelProgressListeners();

        static partial void ValidateProgress();
#if !PROGRESS
        partial class Internal
        {
            public abstract class PromiseWaitPromise<TPromise> : PoolablePromise<TPromise> where TPromise : PromiseWaitPromise<TPromise>
            {
                protected void ClearCachedPromise() { }
            }

            public abstract class PromiseWaitPromise<T, TPromise> : PoolablePromise<T, TPromise> where TPromise : PromiseWaitPromise<T, TPromise>
            {
                protected void ClearCachedPromise() { }
            }
        }

        protected void ReportProgress(float progress) { }

        static protected void ThrowProgressException()
        {
            throw new InvalidOperationException("Define PROGRESS in ProtoPromise/Config.cs to enable progress reports.");
        }

        static partial void ValidateProgress()
        {
            ThrowProgressException();
        }
#else
        protected ValueLinkedStackZeroGC<Internal.IProgressListener> _progressListeners;
        private Internal.UnsignedFixed32 _waitDepthAndProgress;

        partial void ClearProgressListeners()
        {
            _progressListeners.Clear();
        }

        partial void ResolveProgressListeners()
        {
            if (_progressListeners.IsEmpty)
            {
                return;
            }

            // Reverse the order while removing the progress listeners. Back to FIFO.
            var forwardListeners = new ValueLinkedStackZeroGC<Internal.IProgressListener>();
            do
            {
                forwardListeners.Push(_progressListeners.Pop());
            } while (_progressListeners.IsNotEmpty);

            uint increment = _waitDepthAndProgress.GetDifferenceToNextWholeAsUInt32();
            do
            {
                forwardListeners.Pop().ResolveProgress(this, increment);
            } while (forwardListeners.IsNotEmpty);
        }

        partial void RejectProgressListeners()
        {
            while (_progressListeners.IsNotEmpty)
            {
                _progressListeners.Pop().CancelProgressIfOwner(this);
            }
        }

        partial void CancelProgressListeners()
        {
            while (_progressListeners.IsNotEmpty)
            {
                _progressListeners.Pop().CancelProgress();
            }
        }

        protected void ReportProgress(float progress)
        {
            if (progress >= 1f | _state != State.Pending)
            {
                // Don't report progress 1.0, that will be reported automatically when the promise is resolved.
                return;
            }

            uint increment = _waitDepthAndProgress.AssignNewDecimalPartAndGetDifferenceAsUInt32(progress);
            foreach (var progressListener in _progressListeners)
            {
                progressListener.IncrementProgress(increment);
            }
        }

        protected virtual void SetPreviousTo(Promise other) { }

        partial void ResetDepth()
        {
            _waitDepthAndProgress = default(Internal.UnsignedFixed32);
        }

        partial void SetDepthAndPrevious(Promise next)
        {
            next._previous = this;
            next.SetDepth(_waitDepthAndProgress);
        }

        partial void ClearPrevious()
        {
            _previous = null;
        }

        protected virtual void SetDepth(Internal.UnsignedFixed32 previousDepth)
        {
            _waitDepthAndProgress = previousDepth;
        }

        protected virtual bool SubscribeProgressAndContinueLoop(ref Internal.IProgressListener progressListener, out Promise previous)
        {
            _progressListeners.Push(progressListener);
            return (previous = _previous) != null;
        }

        protected virtual bool SubscribeProgressIfWaiterAndContinueLoop(ref Internal.IProgressListener progressListener, out Promise previous, ref ValueLinkedStack<Internal.PromisePassThrough> passThroughs)
        {
            return (previous = _previous) != null;
        }

        protected virtual void SubscribeProgressRoot(Internal.IProgressListener progressListener)
        {
            progressListener.SetInitialAmount(_waitDepthAndProgress);
        }

        protected void ProgressInternal(Action<float> onProgress, int skipFrames)
        {
            ValidateOperation(this);
            ValidateArgument(onProgress, "onProgress");

            if ((byte) _state > 1) // Same as if ( _state == State.Rejected || _state == State.Canceled)
            {
                // Don't report progress if the promise is canceled or rejected.
                return;
            }

            if (_state == State.Resolved)
            {
                var progressHandler = Internal.ResolvedProgressHandler.GetOrCreate(onProgress, skipFrames + 1);
                // Equivalent to calling AddWaiter, but this skips some branches that we already checked here.
                _nextBranches.Enqueue(progressHandler);
                if (!_handling)
                {
                    AddToHandleQueue(this);
                }
                return;
            }

            var progressDelegate = Internal.ProgressDelegate.GetOrCreate(onProgress, this, skipFrames + 1);
            Internal.IProgressListener progressListener = progressDelegate;

            // Directly add to listeners for this promise.
            // Sets promise to the one this is waiting on. Returns false if not waiting on another promise.
            Promise promise;
            if (!SubscribeProgressAndContinueLoop(ref progressListener, out promise))
            {
                // This is the root of the promise tree.
                progressListener.SetInitialAmount(_waitDepthAndProgress);
                return;
            }

            SubscribeProgressToBranchesAndRoots(promise, progressListener);
        }

        private static void SubscribeProgressToBranchesAndRoots(Promise promise, Internal.IProgressListener progressListener)
        {
            // This allows us to subscribe progress to AllPromises and RacePromises iteratively instead of recursively
            ValueLinkedStack<Internal.PromisePassThrough> passThroughs = new ValueLinkedStack<Internal.PromisePassThrough>();

            Repeat:
            SubscribeProgressToChain(promise, progressListener, ref passThroughs);

            if (passThroughs.IsNotEmpty)
            {
                // passThroughs are removed from their targets before adding to passThroughs. Add them back here.
                var passThrough = passThroughs.Pop();
                promise = passThrough.owner;
                progressListener = passThrough.target;
                passThrough.target.ReAdd(passThrough);
                goto Repeat;
            }
        }

        private static void SubscribeProgressToChain(Promise promise, Internal.IProgressListener progressListener, ref ValueLinkedStack<Internal.PromisePassThrough> passThroughs)
        {
            Promise next;
            // If the promise is not waiting on another promise (is the root), it sets next to null, does not add the listener, and returns false.
            // If the promise is waiting on another promise that is not its previous, it adds the listener, transforms progresslistener, sets next to the one it's waiting on, and returns true.
            // Otherwise, it sets next to its previous, adds the listener only if it is a WaitPromise, and returns true.
            while (promise.SubscribeProgressIfWaiterAndContinueLoop(ref progressListener, out next, ref passThroughs))
            {
                promise = next;
            }

            // promise is the root of the promise tree.
            switch (promise._state)
            {
                case State.Pending:
                    {
                        promise.SubscribeProgressRoot(progressListener);
                        break;
                    }
                case State.Resolved:
                    {
                        progressListener.SetInitialAmount(promise._waitDepthAndProgress.GetIncrementedWholeTruncated());
                        break;
                    }
                case State.Rejected:
                    {
                        progressListener.CancelProgressIfOwner(promise);
                        break;
                    }
                default: // case State.Canceled:
                    {
                        progressListener.CancelProgress();
                        break;
                    }
            }
        }

        // Handle progress.
        private static ValueLinkedQueueZeroGC<Internal.IProgressListener> _progressQueue;
        private static bool _runningProgress;

        private static void AddToFrontOfProgressQueue(Internal.IProgressListener progressListener)
        {
            _progressQueue.Push(progressListener);
        }

        private static void AddToBackOfProgressQueue(Internal.IProgressListener progressListener)
        {
            _progressQueue.Enqueue(progressListener);
        }

        private static void HandleProgress()
        {
            if (_runningProgress)
            {
                // HandleProgress is running higher in the program stack, so just return.
                return;
            }

            _runningProgress = true;

            while (_progressQueue.IsNotEmpty)
            {
                _progressQueue.DequeueRisky().Invoke();
            }

            _progressQueue.ClearLast();
            _runningProgress = false;
        }

        partial class Internal
        {
            /// <summary>
            /// Max Whole Number: 2^(32-<see cref="Config.ProgressDecimalBits"/>)
            /// Precision: 1/(2^<see cref="Config.ProgressDecimalBits"/>)
            /// </summary>
            public struct UnsignedFixed32
            {
                private const uint DecimalMax = 1u << Config.ProgressDecimalBits;
                private const uint DecimalMask = DecimalMax - 1u;
                private const uint WholeMask = ~DecimalMask;

                private uint _value;

                public UnsignedFixed32(uint wholePart)
                {
                    _value = wholePart << Config.ProgressDecimalBits;
                }

                public uint WholePart { get { return _value >> Config.ProgressDecimalBits; } }
                public float DecimalPart { get { return (float) DecimalPartAsUInt32 / (float) DecimalMax; } }
                private uint DecimalPartAsUInt32 { get { return _value & DecimalMask; } }

                public uint ToUInt32()
                {
                    return _value;
                }

                public uint AssignNewDecimalPartAndGetDifferenceAsUInt32(float decimalPart)
                {
                    uint oldDecimalPart = DecimalPartAsUInt32;
                    // Don't bother rounding, we don't want to accidentally round to 1.0.
                    uint newDecimalPart = (uint) (decimalPart * DecimalMax);
                    _value = (_value & WholeMask) | newDecimalPart;
                    return newDecimalPart - oldDecimalPart;
                }

                public uint GetDifferenceToNextWholeAsUInt32()
                {
                    return DecimalMax - DecimalPartAsUInt32;
                }

                public UnsignedFixed32 GetIncrementedWholeTruncated()
                {
#if DEBUG
                    checked
#endif
                    {
                        return new UnsignedFixed32()
                        {
                            _value = (_value & WholeMask) + (1u << Config.ProgressDecimalBits)
                        };
                    }
                }

                public void Increment(uint increment)
                {
                    _value += increment;
                }
            }

            // For the special case of adding a progress listener to an already resolved promise.
            public sealed class ResolvedProgressHandler : ITreeHandleable, IStacktraceable
            {
                ITreeHandleable ILinked<ITreeHandleable>.Next { get; set; }
#if DEBUG
                string IStacktraceable.Stacktrace { get; set; }
#endif
                private Action<float> _onProgress;

                private static ValueLinkedStack<ITreeHandleable> _pool;

                static ResolvedProgressHandler()
                {
                    OnClearPool += () => _pool.Clear();
                }

                private ResolvedProgressHandler() { }

                public static ResolvedProgressHandler GetOrCreate(Action<float> onProgress, int skipFrames)
                {
                    var handler = _pool.IsNotEmpty ? (ResolvedProgressHandler) _pool.Pop() : new ResolvedProgressHandler();
                    handler._onProgress = onProgress;
                    SetCreatedStackTrace(handler, skipFrames + 1);
                    return handler;
                }

                void ITreeHandleable.Handle(Promise feed)
                {
                    // Feed is guaranteed to be resolved.
                    var temp = _onProgress;
                    _onProgress = null;
                    if (Config.ObjectPooling != PoolType.None)
                    {
                        _pool.Push(this);
                    }
                    try
                    {
                        temp.Invoke(1f);
                    }
                    catch (Exception e)
                    {
                        UnhandledExceptionException unhandledException = UnhandledExceptionException.GetOrCreate(e);
                        SetStackTraceFromCreated(this, unhandledException);
                        AddRejectionToUnhandledStack(unhandledException);
                    }
                }

                // These will not be called.
                void ITreeHandleable.AssignCancelValue(IValueContainer cancelValue) { throw new InvalidOperationException(); }
                void ITreeHandleable.OnSubscribeToCanceled(IValueContainer cancelValue) { throw new InvalidOperationException(); }
                void ITreeHandleable.Cancel() { throw new InvalidOperationException(); }
                void ITreeHandleable.Repool() { throw new InvalidOperationException(); }
            }

            public interface IProgressListener
            {
                void SetInitialAmount(UnsignedFixed32 amount);
                void Invoke();
                void IncrementProgress(uint amount);
                void ResolveProgress(Promise sender, uint increment);
                void CancelProgressIfOwner(Promise sender);
                void CancelProgress();
            }

            public sealed class ProgressDelegate : IProgressListener, IStacktraceable
            {
#if DEBUG
                string IStacktraceable.Stacktrace { get; set; }
#endif

                private Action<float> _onProgress;
                private Promise _owner;
                private UnsignedFixed32 _current;
                private bool _handling;
                private bool _done;

                private static ValueLinkedStackZeroGC<IProgressListener> _pool;

                static ProgressDelegate()
                {
                    OnClearPool += () => _pool.ClearAndDontRepool();
                }

                private ProgressDelegate() { }

                public static ProgressDelegate GetOrCreate(Action<float> onProgress, Promise owner, int skipFrames)
                {
                    var progress = _pool.IsNotEmpty ? (ProgressDelegate) _pool.Pop() : new ProgressDelegate();
                    progress._onProgress = onProgress;
                    progress._owner = owner;
                    progress._current = default(UnsignedFixed32);
                    SetCreatedStackTrace(progress, skipFrames + 1);
                    return progress;
                }

                private void InvokeAndCatch(Action<float> callback, float progress)
                {
                    try
                    {
                        callback.Invoke(progress);
                    }
                    catch (Exception e)
                    {
                        UnhandledExceptionException unhandledException = UnhandledExceptionException.GetOrCreate(e);
                        SetStackTraceFromCreated(this, unhandledException);
                        AddRejectionToUnhandledStack(unhandledException);
                    }
                }

                void IProgressListener.Invoke()
                {
                    _handling = false;

                    if (_done)
                    {
                        Dispose();
                        return;
                    }

                    // Calculate the normalized progress for the depth that the listener was added.
                    // Divide twice is slower, but gives better precision than single divide.
                    float expected = _owner._waitDepthAndProgress.WholePart + 1u;
                    InvokeAndCatch(_onProgress, ((float) _current.WholePart / expected) + (_current.DecimalPart / expected));
                }

                // This is called by the promise in forward order that listeners were added.
                void IProgressListener.ResolveProgress(Promise sender, uint increment)
                {
                    if (sender == _owner)
                    {
                        var temp = _onProgress;
                        CancelProgress();
                        InvokeAndCatch(temp, 1f);
                    }
                    else
                    {
                        _current.Increment(increment);
                        if (!_handling)
                        {
                            _handling = true;
                            AddToBackOfProgressQueue(this);
                        }
                    }
                }

                void IProgressListener.IncrementProgress(uint amount)
                {
                    _current.Increment(amount);
                    if (!_handling)
                    {
                        _handling = true;
                        // This is called by the promise in reverse order that listeners were added, adding to the front reverses that and puts them in proper order.
                        AddToFrontOfProgressQueue(this);
                    }
                }

                void IProgressListener.SetInitialAmount(UnsignedFixed32 amount)
                {
                    _current = amount;
                    _handling = true;
                    // Always add new listeners to the back.
                    AddToBackOfProgressQueue(this);
                }

                void IProgressListener.CancelProgressIfOwner(Promise sender)
                {
                    if (sender == _owner)
                    {
                        CancelProgress();
                    }
                }

                public void CancelProgress()
                {
                    if (_handling)
                    {
                        // Mark done so InvokeProgressListeners will dispose.
                        _done = true;
                    }
                    else
                    {
                        // Dispose only if it's not in the progress queue.
                        Dispose();
                    }
                }

                private void Dispose()
                {
                    _onProgress = null;
                    _done = false;
                    if (Config.ObjectPooling != PoolType.None)
                    {
                        _pool.Push(this);
                    }
                }
            }

            public abstract class PromiseWaitPromise<TPromise> : PoolablePromise<TPromise>, IProgressListener where TPromise : PromiseWaitPromise<TPromise>
            {
                // This is used to avoid rounding errors when normalizing the progress.
                private UnsignedFixed32 _currentAmount;
                private bool _invokingProgress;
                private bool _secondPrevious;

                protected override void Reset(int skipFrames)
                {
                    base.Reset(skipFrames + 1);
                    _invokingProgress = false;
                    _secondPrevious = false;
                }

                protected override void SubscribeProgressRoot(IProgressListener progressListener)
                {
                    _progressListeners.Push(progressListener);
                    progressListener.SetInitialAmount(_waitDepthAndProgress);
                }

                protected override bool SubscribeProgressAndContinueLoop(ref IProgressListener progressListener, out Promise previous)
                {
                    // This is guaranteed to be pending.
                    _progressListeners.Push(progressListener);
                    previous = _previous;
                    if (_secondPrevious)
                    {
                        bool firstSubscribe = _progressListeners.IsEmpty;
                        if (firstSubscribe)
                        {
                            // Subscribe this to the returned promise.
                            progressListener = this;
                        }
                        return firstSubscribe;
                    }
                    return true;
                }

                protected override bool SubscribeProgressIfWaiterAndContinueLoop(ref IProgressListener progressListener, out Promise previous, ref ValueLinkedStack<PromisePassThrough> passThroughs)
                {
                    if (_state != State.Pending)
                    {
                        previous = null;
                        return false;
                    }
                    return SubscribeProgressAndContinueLoop(ref progressListener, out previous);
                }

                protected override sealed void SetDepth(UnsignedFixed32 previousDepth)
                {
                    _waitDepthAndProgress = previousDepth.GetIncrementedWholeTruncated();
                }

                protected override void SetPreviousTo(Promise other)
                {
                    _secondPrevious = true;
                    _previous = other;
                    if (_progressListeners.IsNotEmpty)
                    {
                        SubscribeProgressToBranchesAndRoots(other, this);
                    }
                }

                void IProgressListener.Invoke()
                {
                    if (_state != State.Pending)
                    {
                        return;
                    }

                    _invokingProgress = false;

                    // Calculate the normalized progress for the depth of the returned promise.
                    // Divide twice is slower, but gives better precision than single divide.
                    float expected = _previous._waitDepthAndProgress.WholePart + 1u;
                    float progress = ((float) _currentAmount.WholePart / expected) + (_currentAmount.DecimalPart / expected);

                    uint increment = _waitDepthAndProgress.AssignNewDecimalPartAndGetDifferenceAsUInt32(progress);

                    foreach (var progressListener in _progressListeners)
                    {
                        progressListener.IncrementProgress(increment);
                    }
                }

                void IProgressListener.SetInitialAmount(UnsignedFixed32 amount)
                {
                    _currentAmount = amount;
                    _invokingProgress = true;
                    // Always add new listeners to the back.
                    AddToBackOfProgressQueue(this);
                }

                void IProgressListener.IncrementProgress(uint amount)
                {
                    _currentAmount.Increment(amount);
                    if (!_invokingProgress)
                    {
                        _invokingProgress = true;
                        // This is called by the promise in reverse order that listeners were added, adding to the front reverses that and puts them in proper order.
                        AddToFrontOfProgressQueue(this);
                    }
                }

                // Not used. The promise handles resolve and cancel.
                void IProgressListener.ResolveProgress(Promise sender, uint increment) { }
                void IProgressListener.CancelProgressIfOwner(Promise sender) { }
                void IProgressListener.CancelProgress() { }
            }

            public abstract class PromiseWaitPromise<T, TPromise> : PoolablePromise<T, TPromise>, IProgressListener where TPromise : PromiseWaitPromise<T, TPromise>
            {
                // This is used to avoid rounding errors when normalizing the progress.
                private UnsignedFixed32 _currentAmount;
                private bool _invokingProgress;
                private bool _secondPrevious;

                protected override void Reset(int skipFrames)
                {
                    base.Reset(skipFrames + 1);
                    _invokingProgress = false;
                }

                protected override void SubscribeProgressRoot(IProgressListener progressListener)
                {
                    _progressListeners.Push(progressListener);
                    progressListener.SetInitialAmount(_waitDepthAndProgress);
                }

                protected override bool SubscribeProgressAndContinueLoop(ref IProgressListener progressListener, out Promise previous)
                {
                    // This is guaranteed to be pending.
                    _progressListeners.Push(progressListener);
                    previous = _previous;
                    if (_secondPrevious)
                    {
                        bool firstSubscribe = _progressListeners.IsEmpty;
                        if (firstSubscribe)
                        {
                            // Subscribe this to the returned promise.
                            progressListener = this;
                        }
                        return firstSubscribe;
                    }
                    return true;
                }

                protected override bool SubscribeProgressIfWaiterAndContinueLoop(ref IProgressListener progressListener, out Promise previous, ref ValueLinkedStack<PromisePassThrough> passThroughs)
                {
                    if (_state != State.Pending)
                    {
                        previous = null;
                        return false;
                    }
                    return SubscribeProgressAndContinueLoop(ref progressListener, out previous);
                }

                protected override sealed void SetDepth(UnsignedFixed32 previousDepth)
                {
                    _waitDepthAndProgress = previousDepth.GetIncrementedWholeTruncated();
                }

                protected override void SetPreviousTo(Promise other)
                {
                    _secondPrevious = true;
                    _previous = other;
                    if (_progressListeners.IsNotEmpty)
                    {
                        SubscribeProgressToBranchesAndRoots(other, this);
                    }
                }

                void IProgressListener.Invoke()
                {
                    if (_state != State.Pending)
                    {
                        return;
                    }

                    _invokingProgress = false;

                    // Calculate the normalized progress for the depth of the cached promise.
                    // Divide twice is slower, but gives better precision than single divide.
                    float expected = _previous._waitDepthAndProgress.WholePart + 1u;
                    float progress = ((float) _currentAmount.WholePart / expected) + (_currentAmount.DecimalPart / expected);

                    uint increment = _waitDepthAndProgress.AssignNewDecimalPartAndGetDifferenceAsUInt32(progress);

                    foreach (var progressListener in _progressListeners)
                    {
                        progressListener.IncrementProgress(increment);
                    }
                }

                void IProgressListener.SetInitialAmount(UnsignedFixed32 amount)
                {
                    _currentAmount = amount;
                    _invokingProgress = true;
                    // Always add new listeners to the back.
                    AddToBackOfProgressQueue(this);
                }

                void IProgressListener.IncrementProgress(uint amount)
                {
                    _currentAmount.Increment(amount);
                    if (!_invokingProgress)
                    {
                        _invokingProgress = true;
                        // This is called by the promise in reverse order that listeners were added, adding to the front reverses that and puts them in proper order.
                        AddToFrontOfProgressQueue(this);
                    }
                }

                // Not used. The promise handles resolve and cancel.
                void IProgressListener.ResolveProgress(Promise sender, uint increment) { }
                void IProgressListener.CancelProgressIfOwner(Promise sender) { }
                void IProgressListener.CancelProgress() { }
            }

            partial class PromiseWaitDeferred<TPromise>
            {
                protected override void SubscribeProgressRoot(IProgressListener progressListener)
                {
                    _progressListeners.Push(progressListener);
                    progressListener.SetInitialAmount(_waitDepthAndProgress);
                }

                protected override bool SubscribeProgressIfWaiterAndContinueLoop(ref IProgressListener progressListener, out Promise previous, ref ValueLinkedStack<PromisePassThrough> passThroughs)
                {
                    if (_previous == null)
                    {
                        previous = null;
                        return false;
                    }
                    return SubscribeProgressAndContinueLoop(ref progressListener, out previous);
                }

                protected override sealed void SetDepth(UnsignedFixed32 previousDepth)
                {
                    _waitDepthAndProgress = previousDepth.GetIncrementedWholeTruncated();
                }
            }

            partial class PromiseWaitDeferred<T, TPromise>
            {
                protected override void SubscribeProgressRoot(IProgressListener progressListener)
                {
                    _progressListeners.Push(progressListener);
                    progressListener.SetInitialAmount(_waitDepthAndProgress);
                }

                protected override bool SubscribeProgressIfWaiterAndContinueLoop(ref IProgressListener progressListener, out Promise previous, ref ValueLinkedStack<PromisePassThrough> passThroughs)
                {
                    if (_previous == null)
                    {
                        previous = null;
                        return false;
                    }
                    return SubscribeProgressAndContinueLoop(ref progressListener, out previous);
                }

                protected override sealed void SetDepth(UnsignedFixed32 previousDepth)
                {
                    _waitDepthAndProgress = previousDepth.GetIncrementedWholeTruncated();
                }
            }
        }
#endif

        private void Resolve()
        {
#if CANCEL
            if (_state == State.Canceled)
            {
                return;
            }
#endif
            AddToHandleQueue(this);
        }

        private void AddWaiter(Internal.ITreeHandleable waiter)
        {
            if ((_state == State.Resolved | _state == State.Rejected) & !_handling)
            {
                // Continue handling if this is resolved or rejected and it's not already being handled.
                _nextBranches.Enqueue(waiter);
                AddToHandleQueue(this);
            }
#if CANCEL
            else if (_state == State.Canceled)
            {
                waiter.OnSubscribeToCanceled(_rejectedOrCanceledValue);
                AddToCancelQueue(waiter);
            }
#endif
            else
            {
                _nextBranches.Enqueue(waiter);
            }
        }

        void Internal.ITreeHandleable.Handle(Promise feed)
        {
#if CANCEL
            if (_state == State.Canceled)
            {
                // Place in the handle queue so it can be repooled.
                AddToHandleQueue(this);
                return;
            }
#endif
            feed._wasWaitedOn = true;
            ClearPrevious();
            try
            {
                Handle(feed);
            }
            catch (Exception e)
            {
                var ex = Internal.UnhandledExceptionException.GetOrCreate(e);
                SetStackTraceFromCreated(this, ex);
#if CANCEL
                if (_state == State.Canceled)
                {
                    AddRejectionToUnhandledStack(ex);
                    // Place in the handle queue so it can be repooled.
                    AddToHandleQueue(this);
                }
                else
#endif
                {
                    RejectInternal(ex);
                }
            }
        }

        protected virtual void OnCancel()
        {
            ClearProgressListeners();

#if CANCEL
            if (_nextBranches.IsNotEmpty)
            {
                // Add safe for first item.
                var next = _nextBranches.DequeueRisky();
                next.AssignCancelValue(_rejectedOrCanceledValue);
                AddToCancelQueue(next);

                // Add risky for remaining items.
                while (_nextBranches.IsNotEmpty)
                {
                    next = _nextBranches.DequeueRisky();
                    next.AssignCancelValue(_rejectedOrCanceledValue);
                    AddToCancelQueueRisky(next);
                }
                _nextBranches.ClearLast();
            }
#endif
        }

        private void ResolveWithStateCheck()
        {
#if CANCEL
            if (_state == State.Canceled)
            {
                AddToHandleQueue(this);
            }
            else
#endif
            {
                ResolveInternal();
            }
        }
    }

    partial class Promise<T>
    {
        // Calls to these get compiled away in RELEASE mode
        static partial void ValidateOperation(Promise<T> promise);
        static partial void ValidateArgument(Delegate del, string argName);
        static partial void ValidateProgress(float progress);
#if DEBUG
        static partial void ValidateProgress(float progress)
        {
            ValidateProgressValue(progress);
        }

        static partial void ValidateOperation(Promise<T> promise)
        {
            promise.ValidateNotDisposed();
        }

        static partial void ValidateArgument(Delegate del, string argName)
        {
            ValidateArg(del, argName);
        }

        public override string ToString()
        {
            return string.Format("Type: Promise<{0}>, Id: {1}, State: {2}", typeof(T), _id, _state);
        }
#else
        public override string ToString()
        {
            return string.Format("Type: Promise<{0}>, State: {1}", typeof(T), _state);
        }
#endif

        // Calls to this get compiled away when CANCEL is defined.
        static partial void ValidateCancel();
#if CANCEL
        public new Promise<T> Canceled(Action onCanceled)
        {
            AddCancelDelegate(onCanceled, 1);
            return this;
        }

        public new Promise<T> Canceled<TCancel>(Action<TCancel> onCanceled)
        {
            AddCancelDelegate(onCanceled, 1);
            return this;
        }
#else
        static partial void ValidateCancel()
        {
            ThrowCancelException();
        }
#endif

        // Calls to these get compiled away when PROGRESS is defined.
        static partial void ValidateProgress();
#if PROGRESS
        public new Promise<T> Progress(Action<float> onProgress)
        {
            ProgressInternal(onProgress, 1);
            return this;
        }
#else
        static partial void ValidateProgress()
        {
            ThrowProgressException();
        }
#endif

        protected void Resolve(T value)
        {
#if CANCEL
            if (_state == State.Canceled)
            {
                return;
            }
#endif
            _value = value;
            AddToHandleQueue(this);
        }

        protected void ResolveWithStateCheck(T value)
        {
#if CANCEL
            if (_state == State.Canceled)
            {
                AddToHandleQueue(this);
            }
            else
#endif
            {
                ResolveInternal(value);
            }
        }
    }

    partial class Promise
    {
        partial class Internal
        {
            partial class FinallyDelegate : IStacktraceable
            {
#if DEBUG
                string IStacktraceable.Stacktrace { get; set; }
#endif
            }

            partial class CancelDelegate : IStacktraceable
            {
#if DEBUG
                string IStacktraceable.Stacktrace { get; set; }
#endif
            }

#pragma warning disable RECS0096 // Type parameter is never used
            partial class CancelDelegate<T> : IStacktraceable
#pragma warning restore RECS0096 // Type parameter is never used
            {
#if DEBUG
                string IStacktraceable.Stacktrace { get; set; }
#endif
            }
        }
    }
}
#pragma warning restore RECS0029 // Warns about property or indexer setters and event adders or removers that do not use the value parameter
#pragma warning restore CS0618 // Type or member is obsolete
#pragma warning restore IDE0034 // Simplify 'default' expression
#pragma warning restore IDE0018 // Inline variable declaration