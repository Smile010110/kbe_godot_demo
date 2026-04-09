public static class ClientRuntimeState
{
	public static void ResetForSceneTransition()
	{
		MainUi.ResetStaticState();
		Player.ResetStaticState();
		PlayerController.ResetStaticState();
		World.ResetStaticState();
	}
}
