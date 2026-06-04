using Godot;
using KBEngine;

public readonly struct KbeCombatState
{
	public static KbeCombatState Empty { get; } = new(0UL, 0UL, 0U, 0U);

	public KbeCombatState(ulong hitPoints, ulong manaPoints, uint attack, uint defense)
	{
		HitPoints = hitPoints;
		ManaPoints = manaPoints;
		Attack = attack;
		Defense = defense;
	}

	public ulong HitPoints { get; }
	public ulong ManaPoints { get; }
	public uint Attack { get; }
	public uint Defense { get; }
}

public readonly struct KbeMotionState
{
	public static KbeMotionState Empty { get; } = new(0);

	public KbeMotionState(byte rawMoveSpeed)
	{
		RawMoveSpeed = rawMoveSpeed;
	}

	public byte RawMoveSpeed { get; }
	public float MoveSpeedUnits => Mathf.Max(0.1f, RawMoveSpeed / 10.0f);
}

public abstract class KbeEntityProtocolState<TEntity> where TEntity : Entity
{
	protected KbeEntityProtocolState(TEntity entity)
	{
		Entity = entity;
	}

	protected TEntity Entity { get; }

	public int EntityId => Entity.id;
	public KbeVector3Value Position => KbeVector3Value.FromProtocol(Entity.position);
	public KbeVector3Value Direction => KbeVector3Value.FromProtocol(Entity.direction);
	public Vector3 WorldPosition => Position.ToGodot();
	public Vector3 WorldRotationDegrees => WorldEntityRotationMapping.ToGodotRotationDegrees(Direction);

	/// <summary>
	/// 本地玩家同步位置时直接写入 Entity.position / direction，
	/// 绕过引擎 setter 以避免触发 onPositionChanged 回调（本地玩家不需要服务端通知自己）。
	/// </summary>
	public void SetWorldTransform(Vector3 worldPosition, Vector3 worldRotationDegrees)
	{
		Entity.position = KbeVector3Value.FromGodot(worldPosition).ToProtocol();
		Entity.direction = WorldEntityRotationMapping.ToProtocolDirection(worldRotationDegrees).ToProtocol();
	}

	protected static string ResolveDisplayName(string name, string fallbackPrefix, int entityId)
	{
		return string.IsNullOrWhiteSpace(name) ? $"{fallbackPrefix} {entityId}" : name;
	}

	protected static KbeCombatState ResolveCombatState(CombatBase combat)
	{
		return combat == null ? KbeCombatState.Empty : new KbeCombatState(combat.hp, combat.mp, 0U, 0U);
	}

	protected static KbeMotionState ResolveMotionState(MotionBase motion)
	{
		return motion == null ? KbeMotionState.Empty : new KbeMotionState(motion.moveSpeed);
	}
}

public sealed class KbePlayerProtocolState : KbeEntityProtocolState<Player>
{
	public KbePlayerProtocolState(Player entity) : base(entity)
	{
	}

	public ulong DatabaseId => Entity.dbid;
	public ushort ServerId => Entity.server_id;
	public ulong RawServerTime => Entity.server_time;
	public ushort Level => Entity.level;
	public byte Role => Entity.role;
	public byte Sex => Entity.sex;
	public uint Exp => 0U;
	public byte SpaceLine => Entity.space_line;
	public uint SpaceUtype => Entity.space_utype;
	public bool IsLocalPlayer => Entity.isPlayer();
	public string DisplayName => ResolveDisplayName(Entity.name, "Player", EntityId);
	public KbeCombatState Combat => ResolveCombatState(Entity.combat);
	public KbeMotionState Motion => ResolveMotionState(Entity.motion);
}

public sealed class KbeMonsterProtocolState : KbeEntityProtocolState<Monster>
{
	public KbeMonsterProtocolState(Monster entity) : base(entity)
	{
	}

	public ulong DatabaseId => Entity.dbid;
	public string DisplayName => ResolveDisplayName(Entity.name, "Monster", EntityId);
	public KbeCombatState Combat => ResolveCombatState(Entity.combat);
	public KbeMotionState Motion => ResolveMotionState(Entity.motion);
}

public sealed class KbeNpcProtocolState : KbeEntityProtocolState<Npc>
{
	public KbeNpcProtocolState(Npc entity) : base(entity)
	{
	}

	public ulong DatabaseId => Entity.dbid;
	public string DisplayName => ResolveDisplayName(Entity.name, "Npc", EntityId);
	public KbeMotionState Motion => ResolveMotionState(Entity.motion);
}
