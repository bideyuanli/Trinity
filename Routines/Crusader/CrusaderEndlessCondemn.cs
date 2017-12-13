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
                { Sets.ArmorOfAkkhan, SetBonus.Third }
            },
            Items = new List<Item>
            {
                Legendary.FrydehrsWrath,
            },
        };

        #endregion

        public TrinityPower GetOffensivePower()
        {
            if (IsSteedCharging)
                return null;

            if (ShouldCondemn())
                return Condemn();

            return null;
        }

        public TrinityPower GetDefensivePower()
        {
            return GetBuffPower();
        }

        public TrinityPower GetBuffPower()
        {
            if (Player.IsInTown)
                return null;
            if (IsSteedCharging)
                return null;
            TrinityPower power;

            if (ShouldAkaratsChampion())
                return AkaratsChampion();

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
        }


        protected override bool ShouldProvoke()
        {
            return false;
        }
        protected override bool ShouldCondemn()
        {
            if (!Skills.Crusader.Condemn.CanCast())
                return false;

            if (!TargetUtil.AnyMobsInRange(15f))
                return false;
            if (Player.PrimaryResource < 8)
                return false;

            if (Skills.Crusader.Punish.IsBuffActive)
                return false;

            return Skills.Crusader.Condemn.TimeSinceUse > 525 || Skills.Crusader.Condemn.TimeSinceUse < 475;
        }

        protected override bool ShouldIronSkin()
        {
            return false;
        }

        protected override bool ShouldLawsOfValor()
        {
            if (!Settings.SpamLawsOfValor)
                return false;
            if (!Skills.Crusader.LawsOfValor.CanCast())
                return false;
            double time_to_holy = TimeToElementStart(Element.Holy);
            if (time_to_holy < 6000 && time_to_holy > 5500)
                return true;
            return false;
        }

        protected override bool ShouldLawsOfJustice()
        {
            return false;
        }

        protected override bool ShouldLawsOfHope()
        {
            return false;
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
            private bool _moveToGroundBuffs;
            [DefaultValue(false)]
            public bool MoveToGroundBuffs
            {
                get { return _moveToGroundBuffs; }
                set { SetField(ref _moveToGroundBuffs, value); }
            }

            private bool _spamCondemn;
            [DefaultValue(true)]
            public bool SpamCondemn
            {
                get { return _spamCondemn; }
                set { SetField(ref _spamCondemn, value); }
            }

            private bool _spamLawsOfValor;
            [DefaultValue(true)]
            public bool SpamLawsOfValor
            {
                get { return _spamLawsOfValor; }
                set { SetField(ref _spamLawsOfValor, value); }
            }

            private bool _spamProvoke;
            [DefaultValue(true)]
            public bool SpamProvoke
            {
                get { return _spamProvoke; }
                set { SetField(ref _spamProvoke, value); }
            }

            private bool _spamIronSkin;
            [DefaultValue(true)]
            public bool SpamIronSkin
            {
                get { return _spamIronSkin; }
                set { SetField(ref _spamIronSkin, value); }
            }

            private int _clusterSize;
            [DefaultValue(1)]
            public int ClusterSize
            {
                get { return _clusterSize; }
                set { SetField(ref _clusterSize, value); }
            }

            private float _emergencyHealthPct;
            [DefaultValue(0.4f)]
            public float EmergencyHealthPct
            {
                get { return _emergencyHealthPct; }
                set { SetField(ref _emergencyHealthPct, value); }
            }

            private SkillSettings _akarats;
            public SkillSettings Akarats
            {
                get { return _akarats; }
                set { SetField(ref _akarats, value); }
            }

            private SkillSettings _steedCharge;
            public SkillSettings SteedCharge
            {
                get { return _steedCharge; }
                set { SetField(ref _steedCharge, value); }
            }

            #region Skill Defaults

            private static readonly SkillSettings AkaratsDefaults = new SkillSettings
            {
                UseMode = UseTime.Always,
                Reasons = UseReasons.Elites | UseReasons.HealthEmergency,
            };

            private static readonly SkillSettings SteedChargeDefaults = new SkillSettings
            {
                UseMode = UseTime.Default,
                Reasons = UseReasons.Blocked
            };

            #endregion

            public override void LoadDefaults()
            {
                base.LoadDefaults();
                Akarats = AkaratsDefaults.Clone();
                SteedCharge = SteedChargeDefaults.Clone();
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
