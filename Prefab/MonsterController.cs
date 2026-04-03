using Godot;

public partial class MonsterController : WorldEntityControllerBase<Monster>
{
	public Monster Monster => EntityView;

	protected override string CharacterBodyPath => "MonsterCharacterBody3D";
	protected override string NameLabelPath => "MonsterCharacterBody3D/HeadInfo/NameLabel";
	protected override string InfoLabelPath => "MonsterCharacterBody3D/HeadInfo/HPLabel";

	public void BindMonster(Monster monster)
	{
		BindEntity(monster);
	}
}
