using Godot;

public enum NameplateStyle
{
	Self,
	Teammate,
	Neutral,
	Monster,
}

public static class NameplatePalette
{
	public static Color Resolve(NameplateStyle style)
	{
		return style switch
		{
			NameplateStyle.Self => new Color("4cd964"),
			NameplateStyle.Teammate => new Color("4a90ff"),
			NameplateStyle.Monster => new Color("ff5a5f"),
			_ => Colors.White,
		};
	}
}
