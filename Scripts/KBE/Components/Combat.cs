namespace KBEngine
{
	public class Combat : CombatBase
	{
		public override void onEnterworld()
		{
			RefreshOwnerPresentation();
		}

		public override void onHpChanged(ulong oldValue)
		{
			RefreshOwnerPresentation();
		}

		public override void onMpChanged(ulong oldValue)
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
