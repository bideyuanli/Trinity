﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using Trinity.Components.Combat;
using Trinity.Framework.Actors.ActorTypes;
using Trinity.Framework.Grid;
using Trinity.Framework.Objects;
using Trinity.Framework.Reference;
using Trinity.Settings;
using Zeta.Bot;
using Zeta.Common;
using Zeta.Game;
using Zeta.Game.Internals.SNO;


namespace Trinity.Framework.Actors.Properties
{
    public class CommonProperties
    {
        private static readonly Regex NameNumberTrimRegex = new Regex(@"-\d+$", RegexOptions.Compiled);

        internal static void Populate(TrinityActor actor)
        {
            var rActor = actor.RActor;
            var commonData = actor.CommonData;

            actor.LastSeenTime = DateTime.UtcNow;
            actor.IsProfileBlacklisted = ProfileManager.CurrentProfile?.TargetBlacklists?.Any(b => b.ActorId == actor.ActorSnoId) ?? false;

            actor.IsExcludedId = GameData.ExcludedActorIds.Contains(actor.ActorSnoId) || GameData.BlackListIds.Contains(actor.ActorSnoId);
            actor.IsExcludedType = GameData.ExcludedActorTypes.Contains(actor.ActorType);
            actor.InternalNameLowerCase = actor.InternalName.ToLower();
            actor.IsAllowedClientEffect = GameData.AllowedClientEffects.Contains(actor.ActorSnoId);
            actor.IsObstacle = GameData.NavigationObstacleIds.Contains(actor.ActorSnoId) || GameData.PathFindingObstacles.ContainsKey(actor.ActorSnoId);

            actor.Name = actor.InternalName; // todo get real name for everything (currently only items have this working)

            if (actor.IsRActorBased)
            {
                var actorInfo = commonData?.ActorInfo;
                if (actorInfo != null && actorInfo.IsValid)
                {
                    actor.GizmoType = actorInfo.GizmoType;
                    actor.AxialRadius = actorInfo.AxialCylinder.Ax1;
                }

                actor.Position = rActor.Position;
                actor.WorldDynamicId = rActor.WorldId; //rActor.WorldDynamicId;
                actor.Radius = rActor.CollisionSphere.Radius;
                actor.CollisionRadius = GameData.CustomObjectRadius.ContainsKey(actor.ActorSnoId)
                    ? GameData.CustomObjectRadius[actor.ActorSnoId]
                    : Math.Max(1f, actor.AxialRadius * 0.60f);
            }

            var type = GetObjectType(
                actor.ActorType,
                actor.ActorSnoId,
                actor.GizmoType,
                actor.InternalName
                );

            actor.Type = type;
            actor.ObjectHash = actor.InternalName + actor.AcdId + actor.RActorId;

            actor.IsDestroyable = actor.Type == TrinityObjectType.Barricade || actor.Type == TrinityObjectType.Destructible;

            actor.IsUnit = type == TrinityObjectType.Unit || actor.ActorType == ActorType.Monster || actor.ActorType == ActorType.Player;
            actor.IsItem = type == TrinityObjectType.Item || actor.ActorType == ActorType.Item;
            actor.IsPlayer = type == TrinityObjectType.Player || actor.ActorType == ActorType.Player;
            actor.IsGizmo = actor.ActorType == ActorType.Gizmo;
            actor.IsMonster = actor.ActorType == ActorType.Monster;

            if (actor.IsAcdBased) // && actor.IsAcdValid)
            {
                //actor.Position = commonData.Position; // ACD is not returning position properly for ground items.
                actor.AnnId = commonData.AnnId;
                actor.AcdId = commonData.ACDId;
                actor.GameBalanceId = commonData.GameBalanceId;
                actor.GameBalanceType = commonData.GameBalanceType;
                actor.FastAttributeGroupId = commonData.FastAttribGroupId;

                var animInfo = commonData.AnimationInfo;
                if (animInfo != null)
                {
                    var animation = commonData.AnimationInfo.Current;
                    actor.Animation = animation; // note, trin objects were doing faster read into animationInfo ?
                    actor.AnimationNameLowerCase = GameData.GetAnimationNameLowerCase(animation); // ?
                    actor.AnimationState = commonData.AnimationState;
                }
                else
                {
                    actor.AnimationNameLowerCase = string.Empty;
                }

                var inventorySlot = ZetaDia.Memory.Read<InventorySlot>(commonData.BaseAddress + 0x114); //actor.AcdItemTemp.InventorySlot;
                actor.IsGroundItem = actor.IsItem && inventorySlot == InventorySlot.None && actor.Position != Vector3.Zero;
            }

            actor.PositionHash = string.Empty + Core.Player.WorldSnoId + actor.Position.X + actor.Position.Y;
            actor.SpecialType = GetSpecialType(actor);

            UpdateDistance(actor);
            actor.RequiredRadiusDistance = GetRequiredRange(actor);

            if (actor.Attributes != null)
            {
                actor.IsBountyObjective = actor.Attributes.IsBountyObjective;
                actor.IsMinimapActive = actor.Attributes.IsMinimapActive || Core.Minimap.MinimapIconAcdIds.Contains(actor.AcdId);
                actor.MinimapIconOverride = actor.Attributes.MinimapIconOverride;
                actor.IsNoDamage = actor.Attributes.IsNoDamage;
                actor.IsQuestMonster = actor.Attributes.IsQuestMonster || actor.Attributes.IsShadowClone;
            }
            else
            {
                actor.AnimationNameLowerCase = string.Empty;
            }

            UpdateLineOfSight(actor);
        }

        private static void UpdateDistance(TrinityActor actor)
        {
            actor.Distance = actor.Position.Distance(Core.Actors.ActivePlayerPosition);
            actor.RadiusDistance = Math.Max(actor.Distance - actor.CollisionRadius, 0f);
        }

        public static void Update(TrinityActor actor)
        {
            if (actor.IsRActorBased)
            {
                actor.Position = actor.RActor.Position;
                UpdateDistance(actor);
            }

            if (actor.IsAcdBased) // && actor.IsAcdValid)
            {
                //actor.Position = commonData.Position; // ACD is not reporting commondata position
                actor.AcdId = actor.CommonData.ACDId;

                UpdateDistance(actor);

                if (!actor.IsItem && actor.Distance < 50f)
                {
                    var animInfo = actor.CommonData.AnimationInfo;
                    if (animInfo != null)
                    {
                        var animation = actor.CommonData.AnimationInfo.Current;
                        actor.Animation = animation; // note, trin objects were doing faster read into animationInfo ?
                        actor.AnimationNameLowerCase = GameData.GetAnimationNameLowerCase(animation); // ?
                        actor.AnimationState = actor.CommonData.AnimationState;
                    }
                    else
                    {
                        actor.AnimationNameLowerCase = string.Empty;
                    }
                    //var animation = commonData.AnimationInfo.Current;
                    //actor.Animation = animation;
                    //actor.AnimationNameLowerCase = GameData.GetAnimationNameLowerCase(animation);
                    //actor.AnimationState = commonData.AnimationState;
                }
            }
            //else if (actor.IsRActorBased)
            //{
            //    actor.Position = actor.RActor.Position;
            //    UpdateDistance(actor);
            //}

            UpdateLineOfSight(actor);
        }

        public static void UpdateLineOfSight(TrinityActor actor)
        {
            if (actor.ActorType == ActorType.Item && actor.InventorySlot != InventorySlot.None)
                return;

            var grid = TrinityGrid.GetUnsafeGrid();
            if (grid == null)
                return;

            if (actor.Position != Vector3.Zero && grid.GridBounds != 0)
            {
                var inLineOfSight = grid.CanRayCast(Core.Player.Position, actor.Position);
                actor.IsInLineOfSight = inLineOfSight;

                if (!actor.HasBeenInLoS && inLineOfSight)
                    actor.HasBeenInLoS = true;

                if (inLineOfSight)
                {
                    actor.IsWalkable = grid.CanRayWalk(actor);

                    if (actor.IsWalkable)
                        actor.HasBeenWalkable = true;
                }
                else
                {
                    actor.IsWalkable = false;
                }
            }
        }

        public static TrinityObjectType GetObjectType(ActorType actorType, int actorSno, GizmoType gizmoType, string internalName)
        {
            if (GameData.ObjectTypeOverrides.ContainsKey(actorSno))
                return GameData.ObjectTypeOverrides[actorSno];

            if (GameData.CursedChestSNO.Contains(actorSno))
                return TrinityObjectType.CursedChest;

            if (GameData.CursedShrineSNO.Contains(actorSno))
                return TrinityObjectType.CursedShrine;

            if (GameData.ShrineSNO.Contains(actorSno))
                return TrinityObjectType.Shrine;

            if (GameData.HealthGlobeSNO.Contains(actorSno))
                return TrinityObjectType.HealthGlobe;

            if (GameData.PowerGlobeSNO.Contains(actorSno))
                return TrinityObjectType.PowerGlobe;

            if (GameData.ProgressionGlobeSNO.Contains(actorSno))
                return TrinityObjectType.ProgressionGlobe;

            if (GameData.GoldSNO.Contains(actorSno))
                return TrinityObjectType.Gold;

            if (GameData.BloodShardSNO.Contains(actorSno))
                return TrinityObjectType.BloodShard;

            if (actorType == ActorType.Item || GameData.ForceToItemOverrideIds.Contains(actorSno))
                return TrinityObjectType.Item;

            if (GameData.AvoidanceSNO.Contains(actorSno))
                return TrinityObjectType.Avoidance;

            if (GameData.ForceTypeAsBarricade.Contains(actorSno))
                return TrinityObjectType.Barricade;

            if (actorType == ActorType.Monster)
                return TrinityObjectType.Unit;

            if (actorType == ActorType.Gizmo)
            {
                switch (gizmoType)
                {
                    case GizmoType.HealingWell:
                        return TrinityObjectType.HealthWell;

                    case GizmoType.Door:
                        return TrinityObjectType.Door;

                    case GizmoType.BreakableDoor:
                        return TrinityObjectType.Barricade;

                    case GizmoType.PoolOfReflection:
                    case GizmoType.PowerUp:
                        return TrinityObjectType.Shrine;

                    case GizmoType.Chest:
                        return TrinityObjectType.Container;

                    case GizmoType.DestroyableObject:
                    case GizmoType.BreakableChest:
                        return TrinityObjectType.Destructible;

                    case GizmoType.PlacedLoot:
                    case GizmoType.LoreChest: // D1 profile Lectern switch to open door
                    case GizmoType.Switch:
                    case GizmoType.Headstone:
                        return TrinityObjectType.Interactable;

                    case GizmoType.Portal:
                        return TrinityObjectType.Portal;

                    case GizmoType.Gate:
                        return TrinityObjectType.Gate;
                }
            }

            if (actorType == ActorType.Environment || actorType == ActorType.Critter || actorType == ActorType.ServerProp)
                return TrinityObjectType.Environment;

            if (actorType == ActorType.Projectile)
                return TrinityObjectType.Projectile;

            if (GameData.BuffedLocationSno.Contains(actorSno))
                return TrinityObjectType.BuffedRegion;

            if (actorType == ActorType.ClientEffect)
                return TrinityObjectType.ClientEffect;

            if (actorType == ActorType.Player)
                return TrinityObjectType.Player;

            if (GameData.PlayerBannerSNO.Contains(actorSno))
                return TrinityObjectType.Banner;

            if (internalName != null && internalName.StartsWith("Waypoint-"))
                return TrinityObjectType.Waypoint;

            return TrinityObjectType.Unknown;
        }

        public static SpecialTypes GetSpecialType(TrinityActor cacheObject)
        {
            if (cacheObject.ActorSnoId == 4860) //SNOActor.PlayerHeadstone
                return SpecialTypes.PlayerTombstone;

            return SpecialTypes.None;
        }

        public static TrinityObjectType GetObjectType(TrinityActor obj)
        {
            return GetObjectType(
                obj.ActorType,
                obj.ActorSnoId,
                obj.GizmoType,
                obj.InternalName
                );
        }

        public static float GetRequiredRange(TrinityActor actor)
        {
            var result = 2f;

            switch (actor.Type)
            {
                // * Unit, we need to pick an ability to use and get within range
                case TrinityObjectType.Unit:
                    {
                        if (actor.IsHidden || actor.IsQuestMonster)
                        {
                            result = actor.CollisionRadius +1;
                        }
                        else
                        {
                            if (TrinityCombat.Targeting.CurrentPower != null)
                                result = Math.Max(TrinityCombat.Targeting.CurrentPower.MinimumRange, actor.CollisionRadius + 1);
                            else
                                result = actor.CollisionRadius +1;
                        }
                        break;
                    }
                // * Item - need to get within 6 feet and then interact with it
                case TrinityObjectType.Item:
                    {
                        result = 5f;
                        break;
                    }
                // * Gold - need to get within pickup radius only
                case TrinityObjectType.Gold:
                    {
                        result = 2f;
                        break;
                    }
                // * Globes - need to get within pickup radius only
                case TrinityObjectType.PowerGlobe:
                case TrinityObjectType.HealthGlobe:
                case TrinityObjectType.ProgressionGlobe:
                    {
                        result = 2f;
                        break;
                    }
                // * Shrine & Container - need to get within 8 feet and interact
                case TrinityObjectType.HealthWell:
                    {
                        result = 4f;

                        float range;
                        if (GameData.CustomObjectRadius.TryGetValue(actor.ActorSnoId, out range))
                        {
                            result = range;
                        }
                        break;
                    }
                case TrinityObjectType.Shrine:
                case TrinityObjectType.Container:
                    {
                        result = 6f;

                        float range;
                        if (GameData.CustomObjectRadius.TryGetValue(actor.ActorSnoId, out range))
                        {
                            result = range;
                        }
                        break;
                    }
                case TrinityObjectType.Interactable:
                    {
                        result = 5f;
                        float range;
                        if (GameData.CustomObjectRadius.TryGetValue(actor.ActorSnoId, out range))
                        {
                            result = range;
                        }
                        if (result <= 0)
                            result = actor.AxialRadius;
                        break;
                    }
                // * Destructible - need to pick an ability and attack it
                case TrinityObjectType.Destructible:
                    {
                        result = actor.CollisionRadius;
                        break;
                    }
                case TrinityObjectType.Barricade:
                    {
                        result = actor.AxialRadius * 0.8f;
                        break;
                    }
                // * Avoidance - need to pick an avoid location and move there
                case TrinityObjectType.Avoidance:
                    {
                        result = 2f;
                        break;
                    }
                case TrinityObjectType.Door:
                    result = Math.Max(2f, actor.AxialRadius);
                    break;
                default:
                    result = actor.Radius;
                    break;
            }
            return result;
        }


    }


}
