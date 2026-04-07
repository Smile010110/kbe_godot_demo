using Godot;

public partial class NpcController : WorldEntityControllerBase<Npc>
{
	public Npc Npc => EntityView;

	protected override string CharacterBodyPath => "NpcCharacterBody3D";
	protected override string NameLabelPath => "NpcCharacterBody3D/HeadInfo/NameLabel";
	protected override string InfoLabelPath => "NpcCharacterBody3D/HeadInfo/HPLabel";

	public void BindNpc(Npc npc)
	{
		BindEntity(npc);
	}
}
