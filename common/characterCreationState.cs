using System;

namespace CommonData
{
	public sealed class CharacterCreationDraft
	{
		public string Name { get; set; } = string.Empty;
		public int Role { get; set; }
		public int Sex { get; set; }
		public uint ModelId { get; set; }
		public bool IsConfirmed { get; set; }

		public CharacterCreationDraft Clone()
		{
			return new CharacterCreationDraft
			{
				Name = Name,
				Role = Role,
				Sex = Sex,
				ModelId = ModelId,
				IsConfirmed = IsConfirmed,
			};
		}
	}

	public static class CharacterCreationState
	{
		private static CharacterCreationDraft _current = BuildDefaultDraft();

		public static CharacterCreationDraft Current => _current;

		public static void Reset()
		{
			_current = BuildDefaultDraft();
		}

		public static void Set(CharacterCreationDraft draft)
		{
			_current = draft?.Clone() ?? BuildDefaultDraft();
		}

		public static CharacterCreationDraft BuildDefaultDraft()
		{
			var role = RoleConfigRepository.GetDefault();
			var sex = SexConfigRepository.GetDefault();
			return new CharacterCreationDraft
			{
				Role = role.Role != 0 ? role.Role : role.Id,
				Sex = sex.Sex,
				ModelId = sex.ModelId,
				IsConfirmed = false,
			};
		}

		public static void EnsureModelResolved(CharacterCreationDraft draft)
		{
			if (draft == null)
			{
				return;
			}

			if (SexConfigRepository.TryGetBySex(draft.Sex, out var sexEntry))
			{
				draft.ModelId = sexEntry.ModelId;
			}
		}
	}
}
