using Godot;

public static class SkillFloatingTextPresenter
{
	private const string LayerName = "RuntimeCombatFloatingTextLayer";
	private const string RootName = "RuntimeCombatFloatingTextRoot";
	private const int LayerIndex = 120;
	private const int DamageFontSize = 34;
	private const int HealFontSize = 32;
	private const int KillFontSizeBonus = 6;
	private const int OutlineSize = 8;
	private const float HeadHeight = 2.35f;
	private const float HorizontalOffset = 34.0f;
	private const float VerticalOffset = -18.0f;
	private const float DriftRight = 52.0f;
	private const float DriftUp = -82.0f;
	private const float PopSeconds = 0.12f;

	private static readonly Vector2 LabelSize = new(220.0f, 76.0f);
	private static readonly Color DamageColor = new(1.0f, 0.12f, 0.08f, 1.0f);
	private static readonly Color HealColor = new(0.24f, 1.0f, 0.30f, 1.0f);
	private static readonly Color OutlineColor = new(0.03f, 0.015f, 0.01f, 0.92f);

	private static CanvasLayer _canvasLayer;
	private static Control _root;
	private static int _spawnSequence;

	public static void ResetStaticState()
	{
		if (_canvasLayer != null && GodotObject.IsInstanceValid(_canvasLayer))
		{
			_canvasLayer.QueueFree();
		}

		_canvasLayer = null;
		_root = null;
		_spawnSequence = 0;
	}

	public static void Show(Node3D anchor, SkillCastResult skillCast, float lifetimeSeconds)
	{
		if (anchor == null || skillCast == null || !EnsureLayer(anchor))
		{
			return;
		}

		var startPosition = ResolveScreenPosition(anchor) + ResolveStackOffset();
		var endPosition = startPosition + new Vector2(DriftRight, DriftUp);
		var label = CreateLabel(skillCast);
		label.Position = startPosition - LabelSize * 0.5f;
		label.PivotOffset = LabelSize * 0.5f;
		label.Scale = new Vector2(0.72f, 0.72f);

		_root.AddChild(label);

		var duration = Mathf.Max(lifetimeSeconds, 0.35f);
		var tween = label.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position", endPosition - LabelSize * 0.5f, duration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "modulate:a", 0.0f, duration)
			.SetDelay(duration * 0.38f)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.TweenProperty(label, "scale", new Vector2(1.18f, 1.18f), PopSeconds)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(label, "scale", Vector2.One, duration - PopSeconds)
			.SetDelay(PopSeconds)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.Out);
		tween.Finished += label.QueueFree;
	}

	private static bool EnsureLayer(Node source)
	{
		if (_canvasLayer != null && GodotObject.IsInstanceValid(_canvasLayer)
			&& _root != null && GodotObject.IsInstanceValid(_root))
		{
			return true;
		}

		var tree = source.GetTree();
		if (tree?.Root == null)
		{
			return false;
		}

		_canvasLayer = new CanvasLayer
		{
			Name = LayerName,
			Layer = LayerIndex,
		};

		_root = new Control
		{
			Name = RootName,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		tree.Root.AddChild(_canvasLayer);
		_canvasLayer.AddChild(_root);
		return true;
	}

	private static Label CreateLabel(SkillCastResult skillCast)
	{
		var label = new Label
		{
			Text = BuildText(skillCast),
			CustomMinimumSize = LabelSize,
			Size = LabelSize,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		label.AddThemeFontSizeOverride("font_size", ResolveFontSize(skillCast));
		label.AddThemeColorOverride("font_color", ResolveColor(skillCast));
		label.AddThemeConstantOverride("outline_size", OutlineSize);
		label.AddThemeColorOverride("font_outline_color", OutlineColor);
		return label;
	}

	private static Vector2 ResolveScreenPosition(Node3D anchor)
	{
		var viewport = anchor.GetViewport();
		var camera = viewport?.GetCamera3D();
		var worldPosition = anchor.GlobalPosition + new Vector3(0.0f, HeadHeight, 0.0f);
		if (camera == null)
		{
			return viewport?.GetVisibleRect().Size * 0.5f ?? Vector2.Zero;
		}

		if (camera.IsPositionBehind(worldPosition))
		{
			return viewport.GetVisibleRect().Size * 0.5f;
		}

		return camera.UnprojectPosition(worldPosition) + new Vector2(HorizontalOffset, VerticalOffset);
	}

	private static Vector2 ResolveStackOffset()
	{
		_spawnSequence = (_spawnSequence + 1) % 5;
		return _spawnSequence switch
		{
			1 => new Vector2(0.0f, -10.0f),
			2 => new Vector2(18.0f, 2.0f),
			3 => new Vector2(-14.0f, -4.0f),
			4 => new Vector2(10.0f, -18.0f),
			_ => Vector2.Zero,
		};
	}

	private static string BuildText(SkillCastResult skillCast)
	{
		var prefix = skillCast.EffectType == SkillEffectType.Heal ? "+" : "-";
		var suffix = skillCast.IsKill ? " KILL" : string.Empty;
		return $"{prefix}{skillCast.Value}{suffix}";
	}

	private static Color ResolveColor(SkillCastResult skillCast)
	{
		return skillCast.EffectType == SkillEffectType.Heal ? HealColor : DamageColor;
	}

	private static int ResolveFontSize(SkillCastResult skillCast)
	{
		var size = skillCast.EffectType == SkillEffectType.Heal ? HealFontSize : DamageFontSize;
		return skillCast.IsKill ? size + KillFontSizeBonus : size;
	}
}
