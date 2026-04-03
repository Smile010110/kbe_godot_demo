namespace KBEngine
{
	public class Motion : MotionBase
	{
		public override void onEnterworld()
		{
			RefreshOwnerPresentation();
		}

		public override void onMoveSpeedChanged(byte oldValue)
		{
			RefreshOwnerPresentation();
		}

		private void RefreshOwnerPresentation()
		{
			if (owner?.renderObj is global::PlayerController playerController)
			{
				playerController.SetHeadInfo();
			}
		}
	}
}
