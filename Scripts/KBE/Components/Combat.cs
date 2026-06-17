namespace KBEngine
{
	public class Combat : CombatBase
	{
		public override void onEnterworld()
		{
			RefreshOwnerPresentation();
		}

		private void RefreshOwnerPresentation()
		{
			if (owner is global::IWorldEntityRenderHooks entity)
			{
				entity.RefreshRenderInfo();
			}
		}
	}
}
