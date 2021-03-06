﻿using Harmony;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZombieLand
{
	public class Hediff_Injury_ZombieBite : Hediff_Injury
	{
		static Color infectionColor = Color.red.SaturationChanged(0.75f);

		private HediffComp_Zombie_TendDuration tendDurationComp;
		public HediffComp_Zombie_TendDuration TendDuration
		{
			get
			{
				if (tendDurationComp == null)
					tendDurationComp = this.TryGetComp<HediffComp_Zombie_TendDuration>();
				return tendDurationComp;
			}
		}

		public override string LabelInBrackets
		{
			get
			{
				var state = TendDuration.GetInfectionState();
				switch (state)
				{
					case InfectionState.BittenHarmless:
						return "NoInfectionRisk".Translate();

					case InfectionState.BittenInfectable:
						var ticksToStart = TendDuration.TicksBeforeStartOfInfection();
						return "HoursBeforeBecomingInfected".Translate(new object[] { Tools.ToHourString(ticksToStart, false) });

					case InfectionState.Infecting:
						var ticksToEnd = TendDuration.TicksBeforeEndOfInfection();
						return "HoursBeforeBecomingAZombie".Translate(new object[] { Tools.ToHourString(ticksToEnd, false) });
				}
				return base.LabelInBrackets;
			}
		}

		public override bool CauseDeathNow()
		{
			if (TendDuration != null && TendDuration.GetInfectionState() == InfectionState.Infected)
				return true;

			return base.CauseDeathNow();
		}

		public void ConvertToZombie()
		{
			var pos = pawn.Position;
			var map = pawn.Map;
			var rot = pawn.Rotation;

			var zombie = ZombieGenerator.GeneratePawn();

			zombie.Name = pawn.Name;
			zombie.gender = pawn.gender;

			var apparelToTransfer = new List<Apparel>();
			pawn.apparel.WornApparelInDrawOrder.Do(apparel =>
			{
				Apparel newApparel;
				if (pawn.apparel.TryDrop(apparel, out newApparel))
					apparelToTransfer.Add(newApparel);
			});

			zombie.ageTracker.AgeBiologicalTicks = pawn.ageTracker.AgeBiologicalTicks;
			zombie.ageTracker.AgeChronologicalTicks = pawn.ageTracker.AgeChronologicalTicks;
			zombie.ageTracker.BirthAbsTicks = pawn.ageTracker.BirthAbsTicks;

			zombie.story.childhood = pawn.story.childhood;
			zombie.story.adulthood = pawn.story.adulthood;
			zombie.story.melanin = pawn.story.melanin;
			zombie.story.crownType = pawn.story.crownType;
			zombie.story.hairDef = pawn.story.hairDef;
			zombie.story.bodyType = pawn.story.bodyType;

			var zTweener = Traverse.Create(zombie.Drawer.tweener);
			var pTweener = Traverse.Create(pawn.Drawer.tweener);
			zTweener.Field("tweenedPos").SetValue(pTweener.Field("tweenedPos").GetValue());
			zTweener.Field("lastDrawFrame").SetValue(pTweener.Field("lastDrawFrame").GetValue());
			zTweener.Field("lastTickSpringPos").SetValue(pTweener.Field("lastTickSpringPos").GetValue());

			ZombieGenerator.AssignNewCustomGraphics(zombie);
			ZombieGenerator.FinalizeZombieGeneration(zombie);
			GenPlace.TryPlaceThing(zombie, pos, map, ThingPlaceMode.Direct, null);
			map.GetGrid().ChangeZombieCount(pos, 1);

			pawn.Kill(null);
			pawn.Corpse.Destroy();

			apparelToTransfer.ForEach(apparel => zombie.apparel.Wear(apparel));
			zombie.Rotation = rot;
			zombie.rubbleCounter = Constants.RUBBLE_AMOUNT;
			zombie.state = ZombieState.Wandering;
			zombie.wasColonist = true;

			string text = "ColonistBecameAZombieDesc".Translate(new object[] { zombie.NameStringShort });
			Find.LetterStack.ReceiveLetter("ColonistBecameAZombieLabel".Translate(), text, LetterDefOf.BadUrgent, zombie);
		}

		public override Color LabelColor
		{
			get
			{
				if (TendDuration != null)
					switch (TendDuration.GetInfectionState())
					{
						case InfectionState.BittenInfectable:
							return new Color(1f, 0.5f, 0f); // orange

						case InfectionState.Infecting:
							return Color.red;
					}
				return base.LabelColor;
			}
		}

		public override void Tick()
		{
			var state = TendDuration.GetInfectionState();
			if (state == InfectionState.Infected)
			{
				ConvertToZombie();
				return;
			}

			if (TendDuration.IsTended && (state >= InfectionState.BittenHarmless && state <= InfectionState.Infecting))
			{
				if (Severity > 0f)
					Severity = Math.Max(0f, Severity - 0.001f);
			}
			else
				base.Tick();
		}

		private bool InfectionLocked()
		{
			return TendDuration != null && TendDuration.GetInfectionState() == InfectionState.Infecting;
		}

		public override float PainFactor
		{
			get
			{
				if (InfectionLocked() == false) return base.PainFactor;
				return this.IsTended() ? 0f : base.PainFactor;
			}
		}

		public override float PainOffset
		{
			get
			{
				if (InfectionLocked() == false) return base.PainOffset;
				return this.IsTended() ? 0f : base.PainOffset;
			}
		}

		public override float SummaryHealthPercentImpact
		{
			get
			{
				if (InfectionLocked() == false) return base.PainFactor;
				return this.IsTended() ? 0f : base.SummaryHealthPercentImpact;
			}
		}

		public override void Heal(float amount)
		{
			if (InfectionLocked() == false)
				base.Heal(amount);
		}

		public override bool TryMergeWith(Hediff other)
		{
			if (InfectionLocked() == false)
				return base.TryMergeWith(other);
			return false;
		}
	}
}
