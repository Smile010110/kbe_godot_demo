using System.Collections.Generic;

public static class SkillClientRuntime
{
	private sealed class PendingItem<T>
	{
		public PendingItem(T value)
		{
			Value = value;
			QueuedAtTickMs = System.Environment.TickCount64;
		}

		public T Value { get; }
		public long QueuedAtTickMs { get; }
	}

	private const int MaxPendingResults = 64;
	private const int MaxPendingErrors = 32;
	private const long PendingItemMaxAgeMs = 10_000L;

	private static readonly Queue<PendingItem<SkillCastResult>> PendingResults = new();
	private static readonly Queue<PendingItem<SkillCastError>> PendingErrors = new();

	public static void ResetStaticState()
	{
		PendingResults.Clear();
		PendingErrors.Clear();
		SkillFloatingTextPresenter.ResetStaticState();
	}

	public static void DispatchResult(SkillCastResult result)
	{
		if (result == null)
		{
			return;
		}

		var controller = PlayerController.LocalInstance;
		if (controller == null || !controller.HandleServerSkillResult(result))
		{
			EnqueuePendingResult(result);
		}
	}

	public static void DispatchError(SkillCastError error)
	{
		if (error == null)
		{
			return;
		}

		var controller = PlayerController.LocalInstance;
		if (controller == null || !controller.HandleServerSkillError(error))
		{
			EnqueuePendingError(error);
		}
	}

	public static void Flush(PlayerController controller)
	{
		if (controller == null)
		{
			return;
		}

		var resultCount = PendingResults.Count;
		for (var i = 0; i < resultCount; i++)
		{
			var result = PendingResults.Dequeue();
			if (IsExpired(result.QueuedAtTickMs))
			{
				continue;
			}

			if (!controller.HandleServerSkillResult(result.Value))
			{
				PendingResults.Enqueue(result);
			}
		}

		var errorCount = PendingErrors.Count;
		for (var i = 0; i < errorCount; i++)
		{
			var error = PendingErrors.Dequeue();
			if (IsExpired(error.QueuedAtTickMs))
			{
				continue;
			}

			if (!controller.HandleServerSkillError(error.Value))
			{
				PendingErrors.Enqueue(error);
			}
		}
	}

	private static void EnqueuePendingResult(SkillCastResult result)
	{
		TrimQueue(PendingResults, MaxPendingResults - 1);
		PendingResults.Enqueue(new PendingItem<SkillCastResult>(result));
	}

	private static void EnqueuePendingError(SkillCastError error)
	{
		TrimQueue(PendingErrors, MaxPendingErrors - 1);
		PendingErrors.Enqueue(new PendingItem<SkillCastError>(error));
	}

	private static void TrimQueue<T>(Queue<T> queue, int maxCount)
	{
		while (queue.Count > maxCount)
		{
			queue.Dequeue();
		}
	}

	private static bool IsExpired(long queuedAtTickMs)
	{
		return System.Environment.TickCount64 - queuedAtTickMs > PendingItemMaxAgeMs;
	}
}
