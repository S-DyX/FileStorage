using System;
using System.Threading;

namespace FileStorage.Core
{
	public interface IRepeater : IDisposable
	{

		void Start(Action<CancellationToken> onRepeat);

		event UnhandledExceptionEventHandler Error;
	}
	public sealed class DisallowConcurrentModeRepeater : IRepeater
	{
		private class OperationsQueue
		{
			private int _operationsQueued;

			private readonly int _maxQueueLength;

			private readonly object _syncLock = new object();

			private readonly Action<CancellationToken> _action;

			public OperationsQueue(int maxQueueLength, Action<CancellationToken> onRepeat)
			{
				this._maxQueueLength = maxQueueLength;
				this._action = onRepeat;
			}


			public void Enqueue()
			{
				lock (_syncLock)
				{
					if (_operationsQueued < this._maxQueueLength)
						_operationsQueued++;
				}
			}


			public Action<CancellationToken> Dequeue()
			{
				lock (_syncLock)
				{
					if (0 == _operationsQueued)
						return null;

					_operationsQueued--;
					return _action;
				}
			}
		}

		private const int MaxQueueLength = 1;

		private readonly TimeSpan _dueTime;

		private readonly TimeSpan _period;

		private OperationsQueue _operationsQueue;

		private bool _isProcessing;

		private Timer _timer;

		private readonly CancellationTokenSource _cancellationTokenSource;

		public DisallowConcurrentModeRepeater(TimeSpan dueTime, TimeSpan period)
		{
			this._dueTime = dueTime;
			this._period = period;
			this._cancellationTokenSource = new CancellationTokenSource();
		}


		public void Dispose()
		{
			_cancellationTokenSource.Cancel();

			this._timer?.Dispose();
		}

		public void Start(Action<CancellationToken> onRepeat)
		{
			if (onRepeat == null)
				throw new ArgumentNullException("onRepeat");

			this._operationsQueue = new OperationsQueue(MaxQueueLength, onRepeat);

			this.CreateTimerOnce();
		}

		public event UnhandledExceptionEventHandler Error;

		private void CreateTimerOnce()
		{
			if (null == this._timer)
			{
				this._timer = new Timer(HandleTimerCallback, null, _dueTime, _period);
			}
		}

		private void HandleTimerCallback(object state)
		{
			try
			{
				this._operationsQueue.Enqueue();
				if (_isProcessing)
					return;

				_isProcessing = true;
				Action<CancellationToken> operation = this._operationsQueue.Dequeue();
				while (null != operation)
				{
					try
					{
						operation(_cancellationTokenSource.Token);
					}
					catch (Exception e)
					{
						RaiseError(e);
					}
					operation = this._operationsQueue.Dequeue();
				}
				_isProcessing = false;
			}
			catch { }

		}

		private void RaiseError(Exception ex)
		{
			var handler = this.Error;
			if (null != handler)
				handler(this, new UnhandledExceptionEventArgs(ex, false));
		}
	}
}
