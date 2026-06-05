using System.Collections.Generic;

public static class SkillClientRuntime
{
	private static readonly Queue<SkillCastResult> PendingResults = new();
	private static readonly Queue<SkillCastError> PendingErrors = new();

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
			PendingResults.Enqueue(result);
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
			PendingErrors.Enqueue(error);
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
			if (!controller.HandleServerSkillResult(result))
			{
				PendingResults.Enqueue(result);
			}
		}

		var errorCount = PendingErrors.Count;
		for (var i = 0; i < errorCount; i++)
		{
			var error = PendingErrors.Dequeue();
			if (!controller.HandleServerSkillError(error))
			{
				PendingErrors.Enqueue(error);
			}
		}
	}
}
