using Godot;

public enum WorldEntityKind
{
	Player,
	Npc,
	Monster,
}

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

public static class WorldEntityNameplateStyleResolver
{
	public static NameplateStyle Resolve(IWorldEntityView entity)
	{
		if (entity == null)
		{
			return NameplateStyle.Neutral;
		}

		if (entity.IsLocallyControlled)
		{
			return NameplateStyle.Self;
		}

		if (entity.EntityKind == WorldEntityKind.Monster)
		{
			return NameplateStyle.Monster;
		}

		if (entity.EntityKind == WorldEntityKind.Player && entity.IsTeammate)
		{
			return NameplateStyle.Teammate;
		}

		return NameplateStyle.Neutral;
	}

	public static Color ResolveColor(IWorldEntityView entity)
	{
		return NameplatePalette.Resolve(Resolve(entity));
	}
}
