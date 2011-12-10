﻿using System.Linq;
using System.Threading;
using Styx.Helpers;
using Styx.Logic;
using Styx.WoWInternals;
using Styx.WoWInternals.World;
using Styx.WoWInternals.WoWObjects;
using TreeSharp;
using Action = TreeSharp.Action;

namespace Hera
{
    public partial class Fpsware
    {
        #region Lifeblood
        public class NeedToLifeblood : Decorator
        {
            public NeedToLifeblood(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                const string spellName = "Lifeblood";

                if (!Utils.CombatCheckOk(spellName, true)) return false;
                if (Self.IsHealthPercentAbove(Settings.LifebloodHealth)) return false;
                if (Self.IsBuffOnMe("Lifeblood")) return false;
                if (Me.GotTarget && !Utils.Adds && Me.HealthPercent > 25 && (Me.HealthPercent * 1.2) > CT.HealthPercent) return false;

                return (Spell.CanCast(spellName));
            }
        }

        public class Lifeblood : Action
        {
            protected override RunStatus Run(object context)
            {
                const string spellName = "Lifeblood";
                bool result = Spell.Cast(spellName);

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Select RAF Target
        public class NeedToSelectRAFTarget : Decorator
        {
            public NeedToSelectRAFTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                CLC.RawSetting = Settings.RAFTarget;
                if (!CLC.IsOkToRun) return false;

                return Me.IsInParty && RaFHelper.Leader != null && RaFHelper.Leader.GotTarget && Me.GotTarget && Me.CurrentTargetGuid != RaFHelper.Leader.CurrentTargetGuid;
            }
        }

        public class SelectRAFTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                RaFHelper.Leader.CurrentTarget.Target();
                Thread.Sleep(250);
                bool result = (Me.GotTarget && Me.CurrentTargetGuid == RaFHelper.Leader.CurrentTargetGuid);

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Use Mana Potion
        public class NeedToUseManaPot : Decorator
        {
            public NeedToUseManaPot(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.CombatCheckOk("", true)) return false;
                if (Me.ManaPercent > Settings.ManaPotion) return false;
                //if (Self.IsPowerPercentAbove((Settings.ManaPotion))) return false;
                if (Inventory.ManaPotions.IsUseable) return true;

                return false;
            }
        }

        public class UseManaPot : Action
        {
            protected override RunStatus Run(object context)
            {
                Inventory.ManaPotions.Use();
                return RunStatus.Success;
            }
        }
        #endregion

        #region Use Health Potion
        public class NeedToUseHealthPot : Decorator
        {
            public NeedToUseHealthPot(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Utils.CombatCheckOk("", true)) return false;
                if (Self.IsHealthPercentAbove(Settings.HealthPotion)) return false;
                if (Inventory.HealthPotions.IsUseable) return true;

                return false;
            }
        }

        public class UseHealthPot : Action
        {
            protected override RunStatus Run(object context)
            {
                Inventory.HealthPotions.Use();
                return RunStatus.Success;
            }
        }
        #endregion

        #region We got aggro during pull
        public class NeedToCheckAggroOnPull : Decorator
        {
            public NeedToCheckAggroOnPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Me.Combat && !Me.Mounted)
                {
                    if (Targeting.Instance.TargetList.Count <= 0) return false;

                    if (Me.GotTarget && Target.IsDistanceMoreThan(10) && !CT.Combat)
                    {
                        return Targeting.Instance.TargetList.Where(mob => mob.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != mob.CurrentTargetGuid).Any();
                    }

                    if (!Me.GotTarget && Me.Combat)
                    {
                        return true;
                    }
                }


                return false;
            }
        }

        public class CheckAggroOnPull : Action
        {
            protected override RunStatus Run(object context)
            {
                foreach (WoWUnit mob in Targeting.Instance.TargetList.Where(mob => mob.CurrentTargetGuid == Me.Guid && Me.CurrentTargetGuid != mob.CurrentTargetGuid))
                {
                    Movement.StopMoving();
                    mob.Target();
                    Thread.Sleep(500);
                    break;
                }

                bool result = Me.GotTarget;
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Face Target
        public class NeedToFaceTarget : Decorator
        {
            public NeedToFaceTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Settings.LazyRaider.Contains("always")) return false;
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                if (Me.IsMoving) return false;
                if (Me.IsSafelyFacing(CT.Location)) return false;

                return Timers.Expired("FaceTarget",650);
            }
        }

        public class FaceTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                CT.Face();
                bool result = true;

                Timers.Reset("FaceTarget");

                //Utils.Log("-Face the target", Utils.Colour("Green")););

                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Face Target - Pull
        public class NeedToFaceTargetPull : Decorator
        {
            public NeedToFaceTargetPull(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Settings.LazyRaider.Contains("always")) return false;
                if (!Me.GotTarget || Me.CurrentTarget.Dead || Me.IsMoving) return false;

                return (!Me.IsSafelyFacing(CT));
            }
        }

        public class FaceTargetPull : Action
        {
            protected override RunStatus Run(object context)
            {
                CT.Face();
                bool result = true;
                Utils.LagSleep();
                Thread.Sleep(250);

                Utils.Log("-Face the target", Utils.Colour("Green"));
                return result ? RunStatus.Success : RunStatus.Failure;
            }
        }
        #endregion

        #region Blacklist Pull Target
        public class NeedToBlacklistPullTarget : Decorator
        {
            public NeedToBlacklistPullTarget(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Settings.LazyRaider.Contains("always")) return false;
                return !Target.IsValidPullTarget;
            }
        }

        public class BlacklistPullTarget : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.Log(string.Format("Bad pull target blacklisting and finding another target."), Utils.Colour("Red"));
                Target.BlackList(100);
                Me.ClearTarget();

                return RunStatus.Success;
            }
        }
        #endregion

        #region Eat
        public class NeedToEat : Decorator
        {
            public NeedToEat(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Me.IsSwimming || Me.Mounted) return false;

                Styx.Logic.Common.Rest.RestPercentageHealth = Settings.RestHealth;

                return (!Self.IsBuffOnMe("Food") && Me.HealthPercent < Settings.RestHealth);
            }
        }

        public class Eat : Action
        {
            protected override RunStatus Run(object context)
            {
                WoWItem food = Styx.Logic.Inventory.Consumable.GetBestFood(false);
                if (food == null) return RunStatus.Failure;

                Utils.Log(string.Format("Eating {0}", food.Name), Utils.Colour("Green"));
                LevelbotSettings.Instance.FoodName = food.Name;
                Styx.Logic.Common.Rest.Feed();

                return RunStatus.Success;
            }
        }
        #endregion

        #region Drink
        public class NeedToDrink : Decorator
        {
            public NeedToDrink(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Me.IsSwimming || Me.Mounted) return false;
                if (Self.IsBuffOnMe("Innervate")) return false; // Druid only
                if (Self.IsBuffOnMe("Dispersion")) return false; // Priest
                if (Self.IsBuffOnMe("Evocation")) return false; // Mage
                
                Styx.Logic.Common.Rest.RestPercentageMana = Settings.RestMana;

                return (!Self.IsBuffOnMe("Drink") && Me.ManaPercent < Settings.RestMana);
            }
        }

        public class Drink : Action
        {
            protected override RunStatus Run(object context)
            {
                WoWItem drink = Styx.Logic.Inventory.Consumable.GetBestDrink(false);
                if (drink == null) return RunStatus.Failure;

                Utils.Log(string.Format("Drinking {0}", drink.Name), Utils.Colour("Green"));
                LevelbotSettings.Instance.DrinkName = drink.Name;
                Styx.Logic.Common.Rest.Feed();

                //if (Self.IsBuffOnMe("Drink")) result = true;

                //return result ? RunStatus.Success : RunStatus.Failure;
                return RunStatus.Success;
            }
        }
        #endregion

        #region Cancel Drink Buff
        public class NeedToCancelDrink : Decorator
        {
            public NeedToCancelDrink(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return Self.IsPowerPercentAbove(98) && Self.IsBuffOnMe("Drink");
            }
        }

        public class CancelDrink : Action
        {
            protected override RunStatus Run(object context)
            {
                Lua.DoString("CancelUnitBuff('player', 'Drink')");
                return RunStatus.Success;
            }
        }
        #endregion

        #region Cancel Food Buff
        public class NeedToCancelFood : Decorator
        {
            public NeedToCancelFood(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return Self.IsHealthPercentAbove(98) && Self.IsBuffOnMe("Food");
            }
        }

        public class CancelFood : Action
        {
            protected override RunStatus Run(object context)
            {
                Lua.DoString("CancelUnitBuff('player', 'Food')");
                return RunStatus.Success;
            }
        }
        #endregion

        #region Distance Check
        public class NeedToCheckDistance : Decorator
        {
            public NeedToCheckDistance(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (Settings.LazyRaider.Contains("always")) return false;
                if (!Me.GotTarget || Me.CurrentTarget.Dead) return false;
                if (!Utils.CombatCheckOk("", false)) return false;

                return Movement.NeedToCheck();
            }
        }

        public class CheckDistance : Action
        {
            protected override RunStatus Run(object context)
            {
                Movement.DistanceCheck();
                return RunStatus.Success;
            }
        }
        #endregion

        #region Auto Attack
        public class NeedToAutoAttack : Decorator
        {
            public NeedToAutoAttack(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return (Me.GotTarget && CT.IsAlive && !Me.IsAutoAttacking);
                //return (Me.GotTarget && CT.IsAlive && !Me.IsAutoAttacking && Target.IsWithinInteractRange);
            }
        }

        public class AutoAttack : Action
        {
            protected override RunStatus Run(object context)
            {
                Utils.AutoAttack(true);

                return RunStatus.Failure;
            }
        }
        #endregion

        #region LOS Check
        public class NeedToLOSCheck : Decorator
        {
            public NeedToLOSCheck(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                if (!Me.GotTarget || CT.Dead) return false;
                if (Target.IsWithinInteractRange) return false;

                bool result = !GameWorld.IsInLineOfSight(Me.Location, Me.CurrentTarget.Location);

                return result;
            }
        }

        public class LOSCheck : Action
        {
            protected override RunStatus Run(object context)
            {
                if (Me.IsInInstance)
                {
                    Movement.MoveTo(1);
                    Thread.Sleep(250);

                    while (!GameWorld.IsInLineOfSight(Me.Location, Me.CurrentTarget.Location))
                    {
                        Movement.MoveTo(1);
                        Thread.Sleep(250);
                    }
                    Movement.StopMoving();
                }
                else
                {
                    /*
                    float distance = (float)CT.Distance2D * 0.5f;

                    Utils.Log(string.Format("We don't have LOS on {0} moving closer...", CT.Name), System.Drawing.Color.Red);
                    Movement.MoveTo(distance);

                    Thread.Sleep(250);
                    while (Me.IsMoving) Thread.Sleep(250);
                     */

                    Movement.MoveTo(1);
                    Thread.Sleep(250);

                    while (!GameWorld.IsInLineOfSight(Me.Location, Me.CurrentTarget.Location))
                    {
                        Movement.MoveTo(1);
                        Thread.Sleep(250);
                    }
                    Movement.StopMoving();
                    Target.Face();
                }

                return RunStatus.Success;
            }
        }
        #endregion

        #region NavPath
        public class NeedToNavPath : Decorator
        {
            public NeedToNavPath(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return !Target.CanGenerateNavPath;
            }
        }

        public class NavPath : Action
        {
            protected override RunStatus Run(object context)
            {
                Target.BlackList(1000);
                Me.ClearTarget();

                return RunStatus.Success;
            }
        }
        #endregion

        #region Mount Check
        public class NeedToMountCheck : Decorator
        {
            public NeedToMountCheck(Composite child) : base(child) { }

            protected override bool CanRun(object context)
            {
                return Me.Mounted;
            }
        }

        public class MountCheck : Action
        {
            protected override RunStatus Run(object context)
            {
                Mount.Dismount();
                return RunStatus.Failure;
            }
        }
        #endregion
    }
}
