using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;
using Trinity.Components.Combat.Resources;
using Trinity.Framework.Actors.ActorTypes;
using Trinity.Framework.Helpers;
using Trinity.Framework.Objects;
using Trinity.Framework.Reference;
using Trinity.UI;
using Zeta.Common;
using Zeta.Game.Internals.Actors;


namespace Trinity.Routines.Crusader
{
    public sealed class CrusaderEndlessCondemn : CrusaderBase, IRoutine
    {
        #region Definition

        public string DisplayName => "圣教军瞬爆天谴";
        public string Description => "具体技能和装备搭配请点下面网页链接,参考搭配.";
        public string Author => "xzjv, QQ小冰";
        public string Version => "0.5";
        public string Url => "https://www.d3planner.com/804462486";

        public Build BuildRequirements => new Build
        {
            Sets = new Dictionary<Set, SetBonus>
            {
                { Sets.SeekerOfTheLight, SetBonus.Third }
            },
            Items = new List<Item>
            {
                Legendary.JohannasArgument,
                Legendary.GuardOfJohanna,
            },
        };

        #endregion

        public TrinityPower GetOffensivePower()
        {
            TrinityPower power;
            TrinityActor target;

            if (IsSteedCharging)
                return null;
            if (TrySpecialPower(out power))
                return power;

            if (!Skills.Crusader.Punish.IsBuffActive && ShouldPunish(out target))
                return Punish(target);

            if (ShouldSlash(out target))
                return Slash(target);
            if (IsNoPrimary)
                return Walk(CurrentTarget);

            return null;
        }

        public TrinityPower GetDefensivePower()
        {
            return GetBuffPower();
        }

        public TrinityPower GetBuffPower()
        {
            TrinityPower power;

            if (IsSteedCharging)
                return null;

            if (ShouldAkaratsChampion())
                return AkaratsChampion();

            if (ShouldProvoke())
                return Provoke();

            //if (ShouldIronSkin())
            //    return IronSkin();

            if (TryLaw(out power))
                return power;

            return null;
        }

        public TrinityPower GetDestructiblePower()
        {
            return DefaultDestructiblePower();
        }

        public TrinityPower GetMovementPower(Vector3 destination)
        {
            return null;
            Vector3 position;

            //Delay settings in ShouldFallingSword will help keep this from being spammed on a Cooldown Pylon
            //Default range of 20 yards increased to 50 yards. It will search for any 5 mob cluster within 50 yards
            if (TargetUtil.AnyMobsInRangeOfPosition(destination, 50f, 5) && ShouldFallingSword(out position))
                return FallingSword(destination);

            if (ShouldSteedCharge())
                return SteedCharge();

            return Walk(destination);
        }

        protected override bool ShouldFallingSword(out Vector3 position)
        {
            position = Vector3.Zero;

            if (!Skills.Crusader.FallingSword.CanCast())
                return false;

            //If your health falls below the Emergency Health Percentage in Trinity > Routine settings, cast falling sword again regardless of delay setting.
            if (Player.CurrentHealthPct < Settings.EmergencyHealthPct)
                return true;

            //Uses the delay [in milliseconds] defined in Trinity > Routines to keep falling sword from being recast too quickly - Added check for mobs being in Range
            if (Skills.Crusader.FallingSword.TimeSinceUse < Settings.FallingSwordDelay && TargetUtil.AnyMobsInRange(Settings.FallingSwordMobsRange))
                return false;

            var target = TargetUtil.GetBestClusterUnit() ?? CurrentTarget;
            if (target != null)
            {
                position = target.Position;
                return true;
            }

            return false;
        }

        protected override bool ShouldBlessedHammer(out TrinityActor target)
        {
            target = null;

            if (!Skills.Crusader.BlessedHammer.CanCast())
                return false;

            //Do not cast Blessed Hammer if Wrath is less than Primary Energy Reserve [default = 25]
            //This will conserve enough energy for Falling Sword to be cast again
            if (Player.PrimaryResource <= PrimaryEnergyReserve)
                return false;

            if (!TargetUtil.AnyMobsInRange(10f))
                return false;

            target = TargetUtil.GetBestClusterUnit() ?? CurrentTarget;
            return target != null;
        }


        protected override bool ShouldProvoke()
        {
            if (!Skills.Crusader.Provoke.CanCast())
                return false;

            if (Player.HasBuff(SNOPower.X1_Crusader_Provoke))
                return false;

            if (!TargetUtil.AnyMobsInRange(15f))
                return false;

            return true;
        }
        protected override bool ShouldCondemn()
        {

            if (!Skills.Crusader.Condemn.CanCast())
                return false;

            if (!TargetUtil.AnyMobsInRange(15f))
                return false;


            return Skills.Crusader.Condemn.TimeSinceUse > 525 || Skills.Crusader.Condemn.TimeSinceUse < 475;
        }
        protected override bool ShouldIronSkin()
        {
            return false;
            if (!Skills.Crusader.IronSkin.CanCast())
                return false;

            if (Player.HasBuff(SNOPower.X1_Crusader_IronSkin))
                return false;

            if (!TargetUtil.AnyMobsInRange(20f))
                return false;

            return true;
        }

        protected override bool ShouldLawsOfValor()
        {
            if (!Skills.Crusader.LawsOfValor.CanCast())
                return false;
            double time_to_holy = TimeToElementStart(Element.Holy);
            if (time_to_holy < 6000 && time_to_holy > 5500)
                return true;
            return false;
        }

        protected override bool ShouldLawsOfJustice()
        {
            if (!Skills.Crusader.LawsOfJustice.CanCast())
                return false;

            if (!TargetUtil.AnyMobsInRange(25f))
                return false;

            return true;
        }

        protected override bool ShouldLawsOfHope()
        {
            if (!Skills.Crusader.LawsOfHope.CanCast())
                return false;

            if (!TargetUtil.AnyMobsInRange(25f))
                return false;

            return true;
        }

        protected override bool ShouldAkaratsChampion()
        {
            if (!Skills.Crusader.AkaratsChampion.CanCast())
                return false;

            double time_to_holy = TimeToElementStart(Element.Holy);
            if (time_to_holy < 12000 && time_to_holy > 11000)
                return true;
            return false;
        }

        #region Settings

        public override int ClusterSize => Settings.ClusterSize;
        public override float EmergencyHealthPct => Settings.EmergencyHealthPct;

        IDynamicSetting IRoutine.RoutineSettings => Settings;
        public CrusaderEndlessCondemnSettings Settings { get; } = new CrusaderEndlessCondemnSettings();

        public sealed class CrusaderEndlessCondemnSettings : NotifyBase, IDynamicSetting
        {
            private SkillSettings _akarats;
            private int _fallingSwordDelay;
            private int _fallingSwordMobsRange;
            private int _clusterSize;
            private float _emergencyHealthPct;

            [DefaultValue(10)]
            public int ClusterSize
            {
                get { return _clusterSize; }
                set { SetField(ref _clusterSize, value); }
            }

            [DefaultValue(0.4f)]
            public float EmergencyHealthPct
            {
                get { return _emergencyHealthPct; }
                set { SetField(ref _emergencyHealthPct, value); }
            }

            public SkillSettings Akarats
            {
                get { return _akarats; }
                set { SetField(ref _akarats, value); }
            }

            [DefaultValue(8000)]
            public int FallingSwordDelay
            {
                get { return _fallingSwordDelay; }
                set { SetField(ref _fallingSwordDelay, value); }
            }

            [DefaultValue(15)]
            public int FallingSwordMobsRange
            {
                get { return _fallingSwordMobsRange; }
                set { SetField(ref _fallingSwordMobsRange, value); }
            }


            #region Skill Defaults

            private static readonly SkillSettings AkaratsDefaults = new SkillSettings
            {
                UseMode = UseTime.Selective,
                Reasons = UseReasons.Elites | UseReasons.HealthEmergency,
            };

            #endregion

            public override void LoadDefaults()
            {
                base.LoadDefaults();
                Akarats = AkaratsDefaults.Clone();
            }

            #region IDynamicSetting

            public string GetName() => GetType().Name;
            public UserControl GetControl() => UILoader.LoadXamlByFileName<UserControl>(GetName() + ".xaml");
            public object GetDataContext() => this;
            public string GetCode() => JsonSerializer.Serialize(this);
            public void ApplyCode(string code) => JsonSerializer.Deserialize(code, this, true);
            public void Reset() => LoadDefaults();
            public void Save() { }

            #endregion
        }

        #endregion
    }
}


