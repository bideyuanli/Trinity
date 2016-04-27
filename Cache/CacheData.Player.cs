﻿using System;
using System.Collections.Generic;
using System.Linq;
using Trinity.Cache;
using Trinity.DbProvider;
using Trinity.Helpers;
using Trinity.Objects;
using Trinity.Reference;
using Trinity.Technicals;
using Zeta.Bot;
using Zeta.Common;
using Zeta.Game;
using Zeta.Game.Internals;
using Zeta.Game.Internals.Actors;
using Zeta.Game.Internals.SNO;
using Logger = Trinity.Technicals.Logger;

namespace Trinity
{
    public partial class CacheData
    {
        /// <summary>
        /// Fast Player Cache, Self-Updating, use instead of ZetaDia.Me / ZetaDia.Cplayer
        /// </summary>
        public class PlayerCache
        {
            static PlayerCache()
            {
                Pulsator.OnPulse += (sender, args) => Instance.UpdatePlayerCache();            
            }

            public PlayerCache()
            {
                HealthHistory = new List<float>();
                UpdatePlayerCache();
            }

            private static PlayerCache _instance;
            public static PlayerCache Instance
            {
                get { return _instance ?? (_instance = new PlayerCache()); }
                set { _instance = value; }
            }

            private IActor _iActor = new TrinityCacheObject();
            public IActor IActor
            {
                get { return _iActor; }
            }

			public int ACDGuid { get; private set; }
            public int RActorGuid { get; private set; }
            public DateTime LastUpdated { get; private set; }
            public bool IsIncapacitated { get; private set; }
            public bool IsRooted { get; private set; }
            public bool IsInRift { get; private set; }
            public double CurrentHealthPct { get; private set; }
            public double PrimaryResource { get; private set; }
            public double PrimaryResourcePct { get; private set; }
            public double PrimaryResourceMax { get; private set; }
            public double PrimaryResourceMissing { get; private set; }
            public double SecondaryResource { get; private set; }
            public double SecondaryResourcePct { get; private set; }
            public double SecondaryResourceMax { get; private set; }
			public double SecondaryResourceMissing { get; private set; }
			public float CooldownReductionPct { get; private set; }
			public float ResourceCostReductionPct { get; private set; }
			public Vector3 Position { get; private set; }
			public int MyDynamicID { get; private set; }
			public int Level { get; private set; }
			public ActorClass ActorClass { get; private set; }
			public string BattleTag { get; private set; }
			public int SceneId { get; private set; }
			public int LevelAreaId { get; private set; }
			public double PlayerDamagePerSecond { get; private set; }
			public SceneInfo Scene { get; private set; }
			public int WorldDynamicID { get; private set; }
			public int WorldID { get; private set; }
			public bool IsInGame { get; private set; }
			public bool IsDead { get; private set; }
			public bool IsLoadingWorld { get; private set; }
			public long Coinage { get; private set; }
			public float GoldPickupRadius { get; private set; }
			public bool IsHidden { get; private set; }
            public long CurrentExperience { get; private set; }
            public long ExperienceNextLevel { get; private set; }
            public long ParagonCurrentExperience { get; private set; }
            public long ParagonExperienceNextLevel { get; private set; }
			public float Rotation { get; private set; }
			public Vector2 DirectionVector { get; private set; }
			public float MovementSpeed { get; private set; }
			public bool IsMoving { get; private set; }
			public bool IsGhosted { get; private set; }
			public bool IsInPandemoniumFortress { get; private set; }
			public GameDifficulty GameDifficulty { get; private set; }
			public TrinityBountyInfo ActiveBounty { get; private set; }
			public bool InActiveEvent { get; private set; }
			public bool HasEventInspectionTask { get; private set; }
			public bool ParticipatingInTieredLootRun { get; private set; }
			public bool IsInTown { get; private set; }
			public bool IsInCombat { get; private set; }
            public int BloodShards { get; private set; }
            public bool IsRanged { get; private set; }
            public bool IsValid { get; private set; }
            public int TieredLootRunlevel { get; private set; }
            public int CurrentQuestSNO { get; private set; }
            public int CurrentQuestStep { get; private set; }
            public Act WorldType { get; private set; }
            public int MaxBloodShards { get; private set; }
            public bool IsMaxCriticalChance { get; set; }
            public bool IsTakingDamage { get; set; }            
            public float CurrentHealth { get; set; }
            public SNOAnim CurrentAnimation { get; set; }
            public bool IsJailed { get; set; }
            public bool IsFrozen { get; set; }
            public bool IsCasting { get; set; }
            public bool IsCastingPortal { get; set; }

            public bool IsInventoryLockedForGreaterRift { get; set; }

            public List<float> HealthHistory { get; set; }

            public class SceneInfo
            {
                public DateTime LastUpdate { get; set; }
                public int SceneId { get; set; }
            }

            internal static DateTime LastSlowUpdate = DateTime.MinValue;
            internal static DateTime LastVerySlowUpdate = DateTime.MinValue;
			internal static DiaActivePlayer _me;

			internal void UpdatePlayerCache()
			{
				using (new PerformanceLogger("UpdateCachedPlayerData"))
				{
					if (DateTime.UtcNow.Subtract(LastUpdated).TotalMilliseconds <= 100)
						return;

					if (!ZetaDia.IsInGame)
					{
                        IsInGame = false;
                        IsValid = false;
						return;
					}

					if (ZetaDia.IsLoadingWorld)
					{
                        IsLoadingWorld = true;
                        IsValid = false;
						return;
					}
                    
					_me = ZetaDia.Me;
					if (_me == null || !_me.IsFullyValid())
					{
                        IsValid = false;
						return;
					}

					try
					{
					    var levelAreaId = ZetaDia.CurrentLevelAreaSnoId;
					    if (levelAreaId != LevelAreaId)
					    {
					        LastChangedLevelAreaId = DateTime.UtcNow;
                            LevelAreaId = levelAreaId;                            
                        }

                        IsValid = true;
                        IsInGame = true;
                        IsLoadingWorld = false;

                        
                        WorldDynamicID = ZetaDia.WorldId;
                        WorldID = ZetaDia.CurrentWorldSnoId;

                        TrinityPlugin.CurrentWorldDynamicId = WorldDynamicID;
                        TrinityPlugin.CurrentWorldId = WorldID;


                        if (DateTime.UtcNow.Subtract(LastVerySlowUpdate).TotalMilliseconds > 5000)
							UpdateVerySlowChangingData();

					    if (DateTime.UtcNow.Subtract(LastSlowUpdate).TotalMilliseconds > 1000)					   
                            UpdateSlowChangingData();								                  

                        UpdateFastChangingData();


                        UpdateIActor();


                    }
					catch (Exception ex)
					{
						Logger.Log(TrinityLogLevel.Debug, LogCategory.CacheManagement, "Safely handled exception for grabbing player data.{0}{1}", Environment.NewLine, ex);
					}
				}
			}

            public DateTime LastChangedLevelAreaId { get; set; }

            private void UpdateIActor()
            {
                _iActor = new TrinityCacheObject
                {
                    Object = _me,
                    CommonData = _me.CommonData,
                    ACDGuid = this.ACDGuid,
                    ActorType = ActorType.Player,
                    ObjectType = ObjectType.Player,
                    IsHostile = false,
                    HitPoints = this.CurrentHealth,
                    HitPointsPct = this.CurrentHealthPct,
                    Rotation = this.Rotation,
                    DynamicID = this.MyDynamicID,                    
                    AnimationState = _me.CommonData.AnimationState,
                    Animation = _me.CommonData.CurrentAnimation,   
                    InternalName = _me.Name,
                    Position = Position,                                     
                };
            }

            internal void UpdateFastChangingData()
			{
                ACDGuid = _me.ACDId;
                RActorGuid = _me.RActorId;
                LastUpdated = DateTime.UtcNow;
                IsInTown = DataDictionary.TownLevelAreaIds.Contains(LevelAreaId);
                IsInRift = DataDictionary.RiftWorldIds.Contains(WorldID);
                IsDead = _me.IsDead;
                IsIncapacitated = (_me.IsFeared || _me.IsStunned || _me.IsFrozen || _me.IsBlind);
                IsRooted = _me.IsRooted;
                CurrentHealthPct = _me.HitpointsCurrentPct;
                PrimaryResource = _me.CurrentPrimaryResource;
                PrimaryResourcePct = PrimaryResource / PrimaryResourceMax;
                PrimaryResourceMissing = PrimaryResourceMax - PrimaryResource;
                SecondaryResource = _me.CurrentSecondaryResource;
                SecondaryResourcePct = SecondaryResource / SecondaryResourceMax;
                SecondaryResourceMissing = SecondaryResourceMax - SecondaryResource;
                Position = _me.Position;
                Rotation = _me.Movement.Rotation;
                DirectionVector = _me.Movement.DirectionVector;
                MovementSpeed = (float)PlayerMover.GetMovementSpeed(); //_me.Movement.SpeedXY;
                IsMoving = _me.Movement.IsMoving;
                IsInCombat = _me.IsInCombat;       
                MaxBloodShards = 500 + ZetaDia.Me.CommonData.GetAttribute<int>(ActorAttributeType.HighestSoloRiftLevel) * 10;
                IsMaxCriticalChance = _me.CritPercentBonusUncapped > 0;
			    IsJailed = _me.HasDebuff(SNOPower.MonsterAffix_JailerCast);
			    IsFrozen = _me.IsFrozen;
                ParticipatingInTieredLootRun = _me.IsParticipatingInTieredLootRun;
                TieredLootRunlevel = _me.InTieredLootRunLevel;
			    IsCasting = _me.LoopingAnimationEndTime > 0;
                CurrentAnimation = _me.CommonData.CurrentAnimation;
                IsInventoryLockedForGreaterRift = ZetaDia.CurrentRift.IsStarted && ZetaDia.CurrentRift.Type == RiftType.Greater && !ZetaDia.CurrentRift.IsCompleted;

                //var direction = ZetaDia.Me.Movement.DirectionVector;
                //         var directionRadians = Math.Atan2(direction.X, direction.Y);
                //var directionDegrees = directionRadians * 180/Math.PI;

                //Logger.LogNormal("Player DirectionVector={0}{1} Radians={2} (DB: {3}) Degrees={4} (DB: {5})",
                //             DirectionVector.X, 
                //             DirectionVector.Y,
                //             directionRadians,
                //             ZetaDia.Me.Movement.Rotation,
                //             directionDegrees,
                //             ZetaDia.Me.Movement.RotationDegrees
                //             );

                var wasCastingPortal = IsCastingPortal;
                IsCastingPortal = IsCasting && wasCastingPortal || IsCastingTownPortalOrTeleport();

                CurrentHealth = _me.HitpointsCurrent;

                HealthHistory.Add(CurrentHealth);
                while (HealthHistory.Count > 5)
                    HealthHistory.RemoveAt(0);

			    var averageHealth = HealthHistory.Average();
                IsTakingDamage = averageHealth  > CurrentHealth;
                if(IsTakingDamage)
                    Logger.LogVerbose(LogCategory.Avoidance, "Taking Damage 5TickAvg={0} Current={1}", averageHealth, CurrentHealth);

                // For WD Angry Chicken
                IsHidden = _me.IsHidden;
			}

            

			internal void UpdateSlowChangingData()
			{
                BloodShards = ZetaDia.PlayerData.BloodshardCount;
                MyDynamicID = _me.CommonData.AnnId;
			    
                //Zeta.Game.ZetaDia.Me.CommonData.GetAttribute<int>(Zeta.Game.Internals.Actors.ActorAttributeType.TieredLootRunRewardChoiceState) > 0;

                Coinage = ZetaDia.PlayerData.Coinage;
                CurrentExperience = (long)ZetaDia.Me.CurrentExperience;

                IsInPandemoniumFortress = DataDictionary.PandemoniumFortressWorlds.Contains(WorldID) ||
                        DataDictionary.PandemoniumFortressLevelAreaIds.Contains(LevelAreaId);

                if (CurrentHealthPct > 0)
                    IsGhosted = _me.CommonData.GetAttribute<int>(ActorAttributeType.Ghosted) > 0;

                if (TrinityPlugin.Settings.Combat.Misc.UseNavMeshTargeting)
                    SceneId = _me.SceneId;

				// Step 13 is used when the player needs to go "Inspect the cursed shrine"
				// Step 1 is event in progress, kill stuff
				// Step 2 is event completed
				// Step -1 is not started
                InActiveEvent = ZetaDia.ActInfo.ActiveQuests.Any(q => DataDictionary.EventQuests.Contains(q.QuestSNO) && q.QuestStep != 13);
                HasEventInspectionTask = ZetaDia.ActInfo.ActiveQuests.Any(q => DataDictionary.EventQuests.Contains(q.QuestSNO) && q.QuestStep == 13);

			    FreeBackpackSlots = _me.Inventory.NumFreeBackpackSlots;

                WorldType = ZetaDia.WorldType;
                if (WorldType != Act.OpenWorld)
                {
                    // Update these only with campaign
                    CurrentQuestSNO = ZetaDia.CurrentQuest.QuestSnoId;
                    CurrentQuestStep = ZetaDia.CurrentQuest.StepId;
                }

				LastSlowUpdate = DateTime.UtcNow;            
			}

			internal void UpdateVerySlowChangingData()
			{
                Level = _me.Level;
                ActorClass = _me.ActorClass;
                BattleTag = FileManager.BattleTagName;
                CooldownReductionPct = ZetaDia.Me.CommonData.GetAttribute<float>(ActorAttributeType.PowerCooldownReductionPercentAll);
                ResourceCostReductionPct = ZetaDia.Me.CommonData.GetAttribute<float>(ActorAttributeType.ResourceCostReductionPercentAll);
                GoldPickupRadius = _me.GoldPickupRadius;
                ExperienceNextLevel = (long)ZetaDia.Me.ExperienceNextLevel;
                //ParagonLevel = ZetaDia.Me.ParagonLevel;
                ParagonCurrentExperience = (long)ZetaDia.Me.ParagonCurrentExperience;
                ParagonExperienceNextLevel = (long)ZetaDia.Me.ParagonExperienceNextLevel;
                //GameDifficulty = ZetaDia.Service.Hero.CurrentDifficulty;
                SecondaryResourceMax = GetMaxSecondaryResource(_me);
			    PrimaryResourceMax = _me.MaxPrimaryResource; //GetMaxPrimaryResource(_me);
			    TeamId = _me.CommonData.TeamId;
			    Radius = _me.CollisionSphere.Radius;
			    IsRanged = ActorClass == ActorClass.Witchdoctor || ActorClass == ActorClass.Wizard || ActorClass == ActorClass.DemonHunter;
			    ElementImmunity = GetElementImmunity();
                LastVerySlowUpdate = DateTime.UtcNow;                
            }

            //private float GetMaxPrimaryResource(DiaActivePlayer player)
            //{
            //    return player.MaxPrimaryResource;
            //}

            private float GetMaxPrimaryResource(DiaActivePlayer player)
            {
                switch (ActorClass)
                {
                    case ActorClass.Wizard:
                        return player.CommonData.GetAttribute<float>(149 | (int)ResourceType.Arcanum << 12) + player.CommonData.GetAttribute<float>(ActorAttributeType.ResourceEffectiveMaxArcanum);
                    case ActorClass.Barbarian:
                        return player.MaxPrimaryResource;
                    case ActorClass.Monk:
                        return player.CommonData.GetAttribute<float>(149 | (int)ResourceType.Spirit << 12) + player.CommonData.GetAttribute<float>(ActorAttributeType.ResourceMaxBonusSpirit);
                    case ActorClass.Crusader:
                        return player.CommonData.GetAttribute<float>(149 | (int)ResourceType.Faith << 12) + player.CommonData.GetAttribute<float>(ActorAttributeType.ResourceMaxBonusFaith);
                    case ActorClass.DemonHunter:
                        return player.CommonData.GetAttribute<float>(149 | (int)ResourceType.Hatred << 12) + player.CommonData.GetAttribute<float>(ActorAttributeType.ResourceMaxBonusHatred);
                    case ActorClass.Witchdoctor:
                        return player.CommonData.GetAttribute<float>(149 | (int)ResourceType.Mana << 12) + player.CommonData.GetAttribute<float>(ActorAttributeType.ResourceMaxBonusMana);
                }
                return -1;
            }

            public HashSet<Element> ElementImmunity = new HashSet<Element>();

            public HashSet<Element> GetElementImmunity()
            {
                var elements = new HashSet<Element>();

                if (Legendary.MarasKaleidoscope.IsEquipped)
                    elements.Add(Element.Poison);

                if (Legendary.TheStarOfAzkaranth.IsEquipped)
                    elements.Add(Element.Fire);

                if (Legendary.TalismanOfAranoch.IsEquipped)
                    elements.Add(Element.Cold);

                if (Legendary.XephirianAmulet.IsEquipped)
                    elements.Add(Element.Lightning);

                if (Legendary.CountessJuliasCameo.IsEquipped)
                    elements.Add(Element.Arcane);

                if (Sets.BlackthornesBattlegear.IsMaxBonusActive)
                {
                    elements.Add(Element.Poison);
                    elements.Add(Element.Fire);
                    elements.Add(Element.Physical);
                }
                return elements;
            }


            //// Item based immunity
            //switch (avoidanceType)
            //{
            //    case AvoidanceType.PoisonTree:
            //    case AvoidanceType.PlagueCloud:
            //    case AvoidanceType.PoisonEnchanted:
            //    case AvoidanceType.PlagueHand:

            //        if (Legendary.MarasKaleidoscope.IsEquipped)
            //        {
            //            Logger.Log(TrinityLogLevel.Debug, LogCategory.Avoidance, "Ignoring Avoidance {0} because MarasKaleidoscope is equipped", avoidanceType);
            //            minAvoidanceHealth = 0;
            //        }
            //        break;

            //    case AvoidanceType.AzmoFireball:
            //    case AvoidanceType.DiabloRingOfFire:
            //    case AvoidanceType.DiabloMeteor:
            //    case AvoidanceType.ButcherFloorPanel:
            //    case AvoidanceType.Mortar:
            //    case AvoidanceType.MageFire:
            //    case AvoidanceType.MoltenTrail:
            //    case AvoidanceType.MoltenBall:
            //    case AvoidanceType.ShamanFire:

            //        if (Legendary.TheStarOfAzkaranth.IsEquipped)
            //        {
            //            Logger.Log(TrinityLogLevel.Debug, LogCategory.Avoidance, "Ignoring Avoidance {0} because TheStarofAzkaranth is equipped", avoidanceType);
            //            minAvoidanceHealth = 0;
            //        }
            //        break;

            //    case AvoidanceType.FrozenPulse:
            //    case AvoidanceType.IceBall:
            //    case AvoidanceType.IceTrail:

            //        // Ignore if both items are equipped
            //        if (Legendary.TalismanOfAranoch.IsEquipped)
            //        {
            //            Logger.Log(TrinityLogLevel.Debug, LogCategory.Avoidance, "Ignoring Avoidance {0} because TalismanofAranoch is equipped", avoidanceType);
            //            minAvoidanceHealth = 0;
            //        }
            //        break;

            //    case AvoidanceType.Orbiter:
            //    case AvoidanceType.Thunderstorm:

            //        if (Legendary.XephirianAmulet.IsEquipped)
            //        {
            //            Logger.Log(TrinityLogLevel.Debug, LogCategory.Avoidance, "Ignoring Avoidance {0} because XephirianAmulet is equipped", avoidanceType);
            //            minAvoidanceHealth = 0;
            //        }
            //        break;

            //    case AvoidanceType.Arcane:
            //        if (Legendary.CountessJuliasCameo.IsEquipped)
            //        {
            //            Logger.Log(TrinityLogLevel.Debug, LogCategory.Avoidance, "Ignoring Avoidance {0} because CountessJuliasCameo is equipped", avoidanceType);
            //            minAvoidanceHealth = 0;
            //        }
            //        break;
            //}

            //// Set based immunity
            //if (Sets.BlackthornesBattlegear.IsMaxBonusActive)
            //{
            //    var blackthornsImmunity = new HashSet<AvoidanceType>
            //    {
            //        AvoidanceType.Desecrator,
            //        AvoidanceType.MoltenBall,
            //        AvoidanceType.MoltenCore,
            //        AvoidanceType.MoltenTrail,
            //        AvoidanceType.PlagueHand
            //    };

            //    if (blackthornsImmunity.Contains(avoidanceType))
            //    {
            //        Logger.Log(TrinityLogLevel.Debug, LogCategory.Avoidance, "Ignoring Avoidance {0} because BlackthornesBattlegear is equipped", avoidanceType);
            //        minAvoidanceHealth = 0;
            //    }
            //}

            public bool IsCastingTownPortalOrTeleport()
            {
                try
                {
                    var commonData = ZetaDia.Me.CommonData;        
                                                   
                    if (CheckVisualEffectNoneForPower(commonData, SNOPower.UseStoneOfRecall))
                    {
                        Logger.LogVerbose("Player is casting 'UseStoneOfRecall'");
                        return true;
                    }

                    //if (CheckVisualEffectNoneForPower(commonData, SNOPower.TeleportToPlayer_Cast))
                    //{
                    //    Logger.LogVerbose("Player is casting 'TeleportToPlayer_Cast'");
                    //    return true;
                    //}
                        
                    if (CheckVisualEffectNoneForPower(commonData, SNOPower.TeleportToWaypoint_Cast))
                    {
                        Logger.LogVerbose("Player is casting 'TeleportToWaypoint_Cast'");
                        return true;
                    }

                    return false;
                }
                catch (Exception) { }
                return false;
            }

            internal bool CheckVisualEffectNoneForPower(ACD commonData, SNOPower power)
            {
                if (commonData.GetAttribute<int>(((int) power << 12) + ((int) ActorAttributeType.PowerBuff0VisualEffectNone & 0xFFF)) == 1)
                    return true;

                return false;
            }
        


            public bool IsCastingOrLoading
            {
                get
                {
                    return
                        ZetaDia.Me != null &&
                        ZetaDia.Me.IsValid &&
                        ZetaDia.Me.CommonData != null &&
                        ZetaDia.Me.CommonData.IsValid &&
                        !ZetaDia.Me.IsDead &&
                        (
                            ZetaDia.IsLoadingWorld ||
                            ZetaDia.Me.CommonData.AnimationState == AnimationState.Casting ||
                            ZetaDia.Me.CommonData.AnimationState == AnimationState.Channeling ||
                            ZetaDia.Me.CommonData.AnimationState == AnimationState.Transform ||
                            ZetaDia.Me.CommonData.AnimationState.ToString() == "13"
                        );
                }
            }

            public object FacingAngle { get; set; }
            public int FreeBackpackSlots { get; set; }
            public int TeamId { get; set; }
            public float Radius { get; set; }

            private float GetMaxSecondaryResource(DiaActivePlayer player)
            {
                switch (ActorClass)
                {
                    case ActorClass.DemonHunter:
                        return ZetaDia.Me.CommonData.GetAttribute<float>(149 | (int)ResourceType.Discipline << 12) + player.CommonData.GetAttribute<float>(ActorAttributeType.ResourceMaxBonusDiscipline);
                }
                return -1;
            }

			public void Clear()
			{
                LastUpdated = DateTime.MinValue;
                LastSlowUpdate = DateTime.MinValue;
                LastVerySlowUpdate = DateTime.MinValue;
                IsIncapacitated = false;
                IsRooted = false;
                IsInTown = false;
                CurrentHealthPct = 0;
                PrimaryResource = 0;
                PrimaryResourcePct = 0;
                SecondaryResource = 0;
                SecondaryResourcePct = 0;
                Position = Vector3.Zero;
                MyDynamicID = -1;
                Level = -1;
                ActorClass = ActorClass.Invalid;
                BattleTag = String.Empty;
                SceneId = -1;
                LevelAreaId = -1;
				Scene = new SceneInfo()
				{
					SceneId = -1,
					LastUpdate = DateTime.UtcNow
				};
			}

            public void ForceUpdates()
            {
                LastUpdated = DateTime.MinValue;
                LastSlowUpdate = DateTime.MinValue;
                LastVerySlowUpdate = DateTime.MinValue;
            }


			public bool IsFacing(Vector3 targetPosition, float arcDegrees = 70f)
			{
				if (DirectionVector != Vector2.Zero)
				{
					Vector3 u = targetPosition - this.Position;
					u.Z = 0f;
					Vector3 v = new Vector3(DirectionVector.X, DirectionVector.Y, 0f);
					bool result = ((MathEx.ToDegrees(Vector3.AngleBetween(u, v)) <= arcDegrees) ? 1 : 0) != 0;
					return result;
				}            
				return false;
			}


        
        }
    }
}
