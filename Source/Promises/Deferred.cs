using System;
using System.Collections.Generic;
using System.Linq;

namespace Promises {
	public class Deferred : Deferred<object> {
	}

	public class Deferred<T> : IPromise<T> {
		private List<PromiseCallback> callbacks = new List<PromiseCallback>();
		protected bool _isResolved;
		protected bool _isRejected;
		private T _arg;

		public Deferred() {
			_isRejected = false;
		}

		public static IPromise When(IEnumerable<IPromise> promises) {
			var count = promises.Count();
			var masterPromise = new Deferred();

			foreach (var p in promises) {
				p.Fail(() => {
					masterPromise.Reject();
				});
				p.Done(() => {
					count--;
					if (0 == count) {
						masterPromise.Resolve();
					}
				});
			}

			return masterPromise;
		}

		public static IPromise When(object d) {
			var masterPromise = new Deferred();
			masterPromise.Resolve();
			return masterPromise;
		}

		public static IPromise When(Deferred d) {
			return d.Promise();
		}

		public static IPromise<T> When(Deferred<T> d) {
			return d.Promise();

		}

		public IPromise<T> Promise() {
			return this;
		}

		public IPromise Always(Action callback) {
			if (_isResolved || _isRejected)
				callback();
			else
				callbacks.Add(new PromiseCallback(callback, PromiseCallback.Condition.Always, false));
			return this;
		}

		public IPromise<T> Always(Action<T> callback) {
			if (_isResolved || _isRejected)
				callback(_arg);
			else
				callbacks.Add(new PromiseCallback(callback, PromiseCallback.Condition.Always, true));
			return this;
		}

		public IPromise<T> Always(IEnumerable<Action<T>> callbacks) {
			foreach (Action<T> callback in callbacks)
				this.Always(callback);
			return this;
		}

		public IPromise Done(Action callback) {
			if (_isResolved)
				callback();
			else
				callbacks.Add(new PromiseCallback(callback, PromiseCallback.Condition.Success, false));
			return this;
		}

		public IPromise<T> Done(Action<T> callback) {
			if (_isResolved)
				callback(_arg);
			else
				callbacks.Add(new PromiseCallback(callback, PromiseCallback.Condition.Success, true));
			return this;
		}

		public IPromise<T> Done(IEnumerable<Action<T>> callbacks) {
			foreach (Action<T> callback in callbacks)
				this.Done(callback);
			return this;
		}

		public IPromise Fail(Action callback) {
			if (_isRejected)
				callback();
			else
				callbacks.Add(new PromiseCallback(callback, PromiseCallback.Condition.Fail, false));
			return this;
		}

		public IPromise<T> Fail(Action<T> callback) {
			if (_isRejected)
				callback(_arg);
			else
				callbacks.Add(new PromiseCallback(callback, PromiseCallback.Condition.Fail, true));
			return this;
		}

		public IPromise<T> Fail(IEnumerable<Action<T>> callbacks) {
			foreach (Action<T> callback in callbacks)
				Fail(callback);
			return this;
		}

		public bool IsRejected {
			get { return _isRejected; }
		}

		public bool IsResolved {
			get { return _isResolved; }
		}

		public bool IsFulfilled {
			get { return _isRejected || _isResolved; }
		}

		public IPromise Reject() {
			if (_isRejected || _isResolved) // ignore if already rejected or resolved
				return this;
			_isRejected = true;
			DequeueCallbacks(PromiseCallback.Condition.Fail);
			return this;
		}

		public Deferred<T> Reject(T arg) {
			if (_isRejected || _isResolved) // ignore if already rejected or resolved
				return this;
			_isRejected = true;
			_arg = arg;
			DequeueCallbacks(PromiseCallback.Condition.Fail);
			return this;
		}

		public IPromise Resolve() {
			if (_isRejected || _isResolved) // ignore if already rejected or resolved
				return this;
			_isResolved = true;
			DequeueCallbacks(PromiseCallback.Condition.Success);
			return this;
		}

		public Deferred<T> Resolve(T arg) {
			if (_isRejected || _isResolved) // ignore if already rejected or resolved
				return this;
			_isResolved = true;
			_arg = arg;
			DequeueCallbacks(PromiseCallback.Condition.Success);
			return this;
		}

		private void DequeueCallbacks(PromiseCallback.Condition cond) {
			foreach (PromiseCallback callback in callbacks) {
				if (callback.Cond == cond || callback.Cond == PromiseCallback.Condition.Always) {
					if (callback.IsReturnValue)
						callback.Del.DynamicInvoke(_arg);
					else
						callback.Del.DynamicInvoke();
				}
			}
			callbacks.Clear();
		}
	}

	internal class PromiseCallback {
		public enum Condition { Always, Success, Fail };

		public PromiseCallback(Delegate del, Condition cond, bool returnValue) {
			Del = del;
			Cond = cond;
			IsReturnValue = returnValue;
		}

		public bool IsReturnValue { get; private set; }
		public Delegate Del { get; private set; }
		public Condition Cond { get; private set; }
	}
}